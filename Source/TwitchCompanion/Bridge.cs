// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ein- und Ausgang zwischen Server und RICS.

using System;
using System.Collections.Concurrent;
using RimWorld;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using TwitchCompanion.RimWorld.Core;
using TwitchCompanion.RimWorld.Rics;
using Verse;

namespace TwitchCompanion.RimWorld
{
    /// <summary>
    /// Verbindet Server (<see cref="ServerLink"/>, Hintergrund) und RICS (Hauptthread). Eingang: Ereignisse des Servers
    /// bzw. Testnachrichten → je Bild an RICS. Ausgang: RICS-Antworten → <see cref="LineMerger"/> → Server bzw. im
    /// Testmodus ins Log.
    /// </summary>
    internal static class Bridge
    {
        private const int MaxIncoming = 200;
        private const int MaxDeliveriesPerFrame = 20;
        private const int MaxEventsPerFrame = 50;
        private const string LogPrefix = "[Twitch Companion] ";
        public const string ServerArg = "twitchcompanion-server";
        private static readonly TimeSpan MaxChatAge = TimeSpan.FromSeconds(60);

        public static readonly LineMerger Outgoing = new LineMerger();
        public static readonly PrivacyNotices Privacy = new PrivacyNotices();
        private static readonly ConcurrentQueue<ChatLine> Incoming = new ConcurrentQueue<ChatLine>();
        private static readonly ConcurrentQueue<string> BackgroundLog = new ConcurrentQueue<string>();

        public static ServerLink Link { get; private set; }
        /// <summary>Nachricht, die RICS gerade verarbeitet (nur Hauptthread, sonst <c>null</c>).</summary>
        public static ChatLine CurrentIncoming { get; private set; }
        public static string LastError { get; private set; }
        public static int Forwarded { get; private set; }
        public static int BlockedByCategory { get; private set; }
        public static int PrivateIgnored { get; private set; }
        public static string LastActivitySummary { get; private set; }

        private static volatile string[] _prefixes = { "!", "$" };
        private static string _serverOverride;
        private static float _nextPrefixCheck;
        private static bool _activityResetDone;
        private static bool _lastLive;
        private static DateTime _lastLiveWrite;

        private static CompanionSettings Settings => TwitchCompanionMod.Settings;

        /// <summary>Server-Adresse; die Befehlszeile <c>-twitchcompanion-server=…</c> hat Vorrang (für Tests mit der Attrappe).</summary>
        public static string ServerUrl => _serverOverride ?? Settings?.serverUrl;
        public static bool ServerUrlOverridden => _serverOverride != null;

        /// <summary>Brücke an, RICS gefunden und Server verbunden (oder Testmodus): Dann gehen RICS-Antworten über die Mod.</summary>
        public static bool IsActive =>
            Settings != null && Settings.enabled && RicsApi.Available && (Settings.testMode || (Link != null && Link.Connected));

        /// <summary>P4: Coin-Vergabe von RICS anhalten, solange die Brücke an ist, aber nicht verbunden oder nicht in RimWorld.</summary>
        public static bool BlocksCoins =>
            Settings != null && Settings.enabled && !Settings.testMode && RicsApi.Available && (Link == null || !Link.GateOpen);

        public static void Start()
        {
            if (Link != null) return;
            if (GenCommandLine.TryGetCommandLineArg(ServerArg, out string url) && !string.IsNullOrWhiteSpace(url))
            {
                _serverOverride = url.Trim();
                Log.Message(LogPrefix + "Server-Adresse aus der Befehlszeile: " + _serverOverride);
            }
            _prefixes = RicsApi.GetPrefixes();
            Link = new ServerLink(BuildConfig, msg => BackgroundLog.Enqueue(msg));
            Link.Start();
        }

        private static LinkConfig BuildConfig()
        {
            CompanionSettings s = Settings;
            if (s == null) return null;
            return new LinkConfig
            {
                Enabled = s.enabled && !s.testMode && RicsApi.Available,
                BaseUrl = ServerUrl,
                Prefixes = _prefixes,
                RequireCategory = s.requireCategory,
                BetBonus = s.betBonus,
            };
        }

        public static void EnqueueIncoming(ChatLine line)
        {
            if (line == null) return;
            Incoming.Enqueue(line);
            while (Incoming.Count > MaxIncoming && Incoming.TryDequeue(out _)) { }
        }

        /// <summary>Ausgang; fest eingebaute englische RICS-Texte werden vorher deutsch und kurz (abschaltbar).</summary>
        public static void EnqueueOutgoing(string text)
        {
            string t = Settings != null && Settings.germanChat ? ChatTranslator.Translate(text) : text;
            // Während RICS im Hauptthread eine Nachricht bearbeitet, werden die Antworten gesammelt und als eine Einblendung gezeigt.
            if (_capturing && Thread.CurrentThread.ManagedThreadId == _mainThreadId) { Captured.Add(t); return; }
            Outgoing.Add(t, DateTime.UtcNow);
        }

        private static readonly List<string> Captured = new List<string>();
        private static bool _capturing;
        private static int _mainThreadId = -1;
        public static int NotAllowed { get; private set; }
        public static int PawnBlocked { get; private set; }
        public static int QueueRejoinBlocked { get; private set; }
        public static int SpamIgnored { get; private set; }
        /// <summary>Letzte Liste plaudernder Chatter (ohne Ignorierte), für den Raid-Beitritt.</summary>
        public static List<ActiveChatter> LastActiveChatters { get; private set; } = new List<ActiveChatter>();
        /// <summary>Verwarnungen wegen Spam; Stand liegt in den Einstellungen (nur IDs).</summary>
        public static readonly SpamGuard Spam = new SpamGuard();
        private static bool _spamLoaded;

        /// <summary>Antwort im Spiel einblenden (sichtbar im Stream) statt im Chat; nur Hauptthread.</summary>
        private static void ShowInGame(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Messages.Message(text, MessageTypeDefOf.SilentInput, true);
            if (Settings.testMode) Log.Message(LogPrefix + "Im Spiel: " + text);
        }

        /// <summary>Einzelne Antwort an einen Zuschauer: im Spiel oder (ohne die Einstellung) im Chat.</summary>
        private static void Reply(ChatLine line, string text)
        {
            if (Settings.repliesInGame) ShowInGame("@" + line.Login + ": " + text);
            else EnqueueOutgoing("@" + line.Login + " " + text);
        }

        /// <summary>Im Hauptthread aufrufen (GameComponent). Liefert Eingänge an RICS und gibt Ausgänge weiter.</summary>
        public static void MainThreadTick()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            while (BackgroundLog.TryDequeue(out string msg)) Log.Message(LogPrefix + msg);

            if (!_activityResetDone && RicsApi.Available && Find.TickManager != null)
            {
                _activityResetDone = true;
                if (Settings.enabled) Raids.CheckSettingsOnLoad();
                if (Settings.enabled && !Settings.testMode)
                {
                    try
                    {
                        int n = RicsApi.ResetLoadedActivity();
                        if (n > 0) Log.Message(LogPrefix + n + " Zuschauer ohne Nachricht seit dem Start gelten nicht mehr als aktiv (keine Coins für Lurker).");
                    }
                    catch (Exception ex) { Log.Warning(LogPrefix + "Aktivität nicht zurückgesetzt: " + (ex.InnerException ?? ex).Message); }
                }
            }

            if (Link != null)
            {
                RefreshPrefixes();
                TrackLive();
                for (int i = 0; i < MaxEventsPerFrame && Link.Events.TryDequeue(out object ev); i++)
                    HandleServerEvent(ev);
            }

            for (int i = 0; i < MaxDeliveriesPerFrame && Incoming.TryDequeue(out ChatLine line); i++)
                Deliver(line);

            try { EventVotes.Tick(); }
            catch (Exception ex) { LastError = (ex.InnerException ?? ex).Message; Log.ErrorOnce(LogPrefix + "Ereignis-Abstimmung: " + ex, 0x7C0AA); }

            foreach (string message in Outgoing.Flush(DateTime.UtcNow))
                Send(message);

            int dropped = Outgoing.TakeDroppedCount();
            if (dropped > 0) Log.Warning(LogPrefix + dropped + " Chatzeile(n) verworfen, zu viele warteten.");
        }

        private static void RefreshPrefixes()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < _nextPrefixCheck) return;
            _nextPrefixCheck = now + 5f;
            string[] current = RicsApi.GetPrefixes();
            if (string.Join(",", current) != string.Join(",", _prefixes)) _prefixes = current; // neue Anmeldung folgt von selbst
        }

        private static void TrackLive()
        {
            ChannelInfo channel = Link.Channel;
            if (channel == null) return;
            if (channel.Live && !_lastLive) Privacy.Reset(); // Hinweis an !privat-Zuschauer einmal je Stream
            _lastLive = channel.Live;

            // Warteschlange nach dem Stream leeren, anstellen wieder frei (Nutzer, 20.11.2026).
            DateTime? lastLive = Settings.LastLiveUtc;
            DateTime now = DateTime.UtcNow;
            bool changed = QueueRules.StreamChanged(channel.Live, now, ref lastLive);
            Settings.LastLiveUtc = lastLive;
            if (changed)
            {
                Settings.queueJoinedThisStream?.Clear();
                Settings.streamStartedTicks = channel.Live ? now.Ticks : 0;
                EventVotes.OnNewStream();
                Spam.Reset();
                _spamLoaded = true;
                Settings.spamStrikes = new List<string>();
                bool cleared = RicsQueue.Clear();
                Log.Message(LogPrefix + (channel.Live ? "Neuer Stream" : "Stream beendet") + ": Warteschlange "
                    + (cleared ? "geleert" : "nicht geleert (kein Spielstand)") + ", anstellen wieder frei.");
            }
            if (changed || now - _lastLiveWrite > TimeSpan.FromMinutes(5)) // Zeitpunkt überlebt einen Neustart von RimWorld
            {
                _lastLiveWrite = now;
                Settings.Write();
            }
        }

        private static void HandleServerEvent(object ev)
        {
            switch (ev)
            {
                case HelloEvent hello:
                    Log.Message(LogPrefix + (hello.Reconnect ? "Wieder mit dem Server verbunden" : "Mit dem Server verbunden")
                        + (hello.Info?.Version != null ? " (Version " + hello.Info.Version + ")" : string.Empty)
                        + ", angemeldet: " + string.Join(" ", _prefixes) + ". Verpasste Nachrichten liefert der Server nicht nach.");
                    break;
                case ChatEvent chat:
                    if (!Link.GateOpen) { BlockedByCategory++; return; }
                    if (DateTime.UtcNow - chat.QueuedAtUtc > MaxChatAge) return; // z. B. im Hauptmenü eingegangen
                    Deliver(chat.Line);
                    break;
                case PrivacyEvent privacy:
                    // Keine Namen ins Log (Spec, „!privat“). Rückwirkendes Entfernen ist offen (OFFEN.md #11).
                    Log.Message(LogPrefix + "Ein Zuschauer hat !privat " + (privacy.Change.Private ? "eingeschaltet" : "ausgeschaltet") + ".");
                    break;
                case PredictionEvent prediction:
                    AwardBetBonus(prediction);
                    break;
                case PollStartEvent pollStart:
                    EventVotes.OnPollStart(pollStart);
                    break;
                case PollResultEvent pollResult:
                    EventVotes.OnPollResult(pollResult);
                    break;
                case RaidEvent raid:
                    Raids.OnRaid(raid.Raid, LastActiveChatters);
                    break;
                case ActiveEvent active:
                    if (!Link.GateOpen || !RicsApi.Available || Find.TickManager == null) return;
                    // Wer nach 3 Verwarnungen ignoriert wird, bekommt bis Streamende auch keine Pfennig.
                    LastActiveChatters = active.Chatters.Where(c => !Spam.IsIgnored(c.UserId)).ToList();
                    var (refreshed, created) = RicsApi.RefreshActivity(LastActiveChatters);
                    LastActivitySummary = DateTime.Now.ToString("HH:mm") + ": " + refreshed + " von " + active.TotalOnServer
                        + " aktiven Chattern aufgefrischt" + (created > 0 ? ", " + created + " neu in RICS" : string.Empty);
                    break;
            }
        }

        /// <summary>Wettbonus (Feature-Issue #11): einmal je Wette, auch über Neustarts hinweg (IDs in den Mod-Einstellungen).</summary>
        private static void AwardBetBonus(PredictionEvent ev)
        {
            if (!Settings.betBonus || !IsActive || !Link.GateOpen || Find.TickManager == null) return;
            if (!Settings.MarkPredictionAwarded(ev.Result.Id)) return;
            try
            {
                var awards = BetBonus.Compute(ev.Result, RicsEconomy.CoinsPerHour, ev.PrivateUserIds);
                foreach (BonusAward a in awards) RicsEconomy.GiveCoins(a.UserId, a.Login, a.Pfennig);
                if (awards.Count > 0)
                {
                    RicsEconomy.SaveViewers();
                    EnqueueOutgoing(BetBonus.Announcement(awards, RicsEconomy.CurrencyName));
                }
                Log.Message(LogPrefix + "Wettbonus: " + awards.Count + " Gewinner, zusammen " + awards.Sum(x => x.Pfennig) + " Pfennig.");
            }
            catch (Exception ex)
            {
                LastError = (ex.InnerException ?? ex).Message;
                Log.Error(LogPrefix + "Wettbonus fehlgeschlagen: " + (ex.InnerException ?? ex));
            }
        }

        private static void Deliver(ChatLine line)
        {
            if (!IsActive || !line.IsComplete) return;

            if (line.IsPrivate)
            {
                PrivateIgnored++;
                // Im Spiel keinen Namen eines !privat-Zuschauers einblenden.
                if (!Settings.repliesInGame && Privacy.ShouldNotify(line.UserId))
                    EnqueueOutgoing("@" + line.Login + " " + PrivacyNotices.Text);
                return;
            }

            // Ausmisten (Nutzer, 20.11.2026): Zuschauer nur erlaubte Befehle, still verworfen; Moderatoren und Streamer alles.
            bool privileged = line.IsBroadcaster || line.IsModerator;

            // Spamschutz (Nutzer, 20.11.2026): zählt jeden Befehl, auch nicht erlaubte; Moderatoren und Streamer nicht.
            if (!privileged)
            {
                if (!_spamLoaded) { Spam.Load(Settings.spamStrikes); _spamLoaded = true; }
                SpamVerdict verdict = Spam.Record(line.UserId, DateTime.UtcNow);
                if (verdict == SpamVerdict.Ignored) { SpamIgnored++; return; }
                if (verdict == SpamVerdict.Strike)
                {
                    Settings.spamStrikes = Spam.Save();
                    Settings.Write();
                    Reply(line, SpamGuard.Message(Spam.StrikesOf(line.UserId)));
                    return;
                }
            }
            if (!CommandFilter.IsAllowed(line.Text, CommandFilter.ParseList(Settings.allowedCommands), privileged)) { NotAllowed++; return; }

            string word = CommandFilter.CommandWord(line.Text);
            int? coinsBefore = null;
            if (word == "pawn")
            {
                line.Text = CommandFilter.StripPawnArguments(line.Text);
                TimeSpan left = PawnGate.Remaining(Settings.LastPawnPurchaseUtc, DateTime.UtcNow, TimeSpan.FromMinutes(Settings.pawnCooldownMinutes));
                if (left > TimeSpan.Zero && !privileged) { PawnBlocked++; Reply(line, PawnGate.Message(left)); return; }
                coinsBefore = RicsEconomy.CoinsOf(line);
            }

            if (word == "joinqueue" && Settings.HasJoinedQueue(line.UserId) && !RicsQueue.IsInQueue(line.NameForRics))
            {
                QueueRejoinBlocked++;
                Reply(line, QueueRules.AlreadyJoined);
                return;
            }

            bool lootboxOpened = false;
            Captured.Clear();
            _capturing = Settings.repliesInGame;
            try
            {
                CurrentIncoming = line;
                if (Settings.autoLootbox)
                {
                    string opened = RicsEconomy.AwardAndOpenLootboxes(line);
                    if (opened != null) { EnqueueOutgoing("@" + line.Login + " " + opened); lootboxOpened = true; }
                }
                RicsApi.Deliver(line);
                Forwarded++;
            }
            catch (Exception ex)
            {
                LastError = (ex.InnerException ?? ex).Message;
                Log.Error(LogPrefix + "Nachricht konnte nicht an RICS gehen: " + (ex.InnerException ?? ex));
            }
            finally
            {
                CurrentIncoming = null;
                _capturing = false;
            }

            if (word == "pawn" && coinsBefore != null && RicsEconomy.CoinsOf(line) < coinsBefore)
            {
                Settings.MarkPawnPurchase(DateTime.UtcNow);
                RicsQueue.Remove(line.NameForRics); // jeder hat höchstens einen Kolonisten
            }
            if (word == "joinqueue" && RicsQueue.IsInQueue(line.NameForRics))
                Settings.MarkJoinedQueue(line.UserId);

            if (Settings.repliesInGame)
            {
                string balance = word == "buy" || word == "pawn" || lootboxOpened ? RicsEconomy.BalanceFor(line) : null;
                ShowInGame(ReplyComposer.Compose(line.Login, Captured, balance));
                Captured.Clear();
            }
        }

        private static void Send(string message)
        {
            if (Settings.repliesInGame) { ShowInGame(message); return; }
            if (Settings.testMode || Link == null)
            {
                Log.Message(LogPrefix + "Chat ← " + message);
                return;
            }
            Link.EnqueueSend(message);
        }
    }
}
