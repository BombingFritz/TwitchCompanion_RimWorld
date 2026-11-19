// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ein- und Ausgang zwischen Server und RICS.

using System;
using System.Collections.Concurrent;
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
            };
        }

        public static void EnqueueIncoming(ChatLine line)
        {
            if (line == null) return;
            Incoming.Enqueue(line);
            while (Incoming.Count > MaxIncoming && Incoming.TryDequeue(out _)) { }
        }

        public static void EnqueueOutgoing(string text) => Outgoing.Add(text, DateTime.UtcNow);

        /// <summary>Im Hauptthread aufrufen (GameComponent). Liefert Eingänge an RICS und gibt Ausgänge weiter.</summary>
        public static void MainThreadTick()
        {
            while (BackgroundLog.TryDequeue(out string msg)) Log.Message(LogPrefix + msg);

            if (!_activityResetDone && RicsApi.Available && Find.TickManager != null)
            {
                _activityResetDone = true;
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
                case ActiveEvent active:
                    if (!Link.GateOpen || !RicsApi.Available || Find.TickManager == null) return;
                    var (refreshed, created) = RicsApi.RefreshActivity(active.Chatters);
                    LastActivitySummary = DateTime.Now.ToString("HH:mm") + ": " + refreshed + " von " + active.TotalOnServer
                        + " aktiven Chattern aufgefrischt" + (created > 0 ? ", " + created + " neu in RICS" : string.Empty);
                    break;
            }
        }

        private static void Deliver(ChatLine line)
        {
            if (!IsActive || !line.IsComplete) return;

            if (line.IsPrivate)
            {
                PrivateIgnored++;
                if (Privacy.ShouldNotify(line.UserId))
                    EnqueueOutgoing("@" + line.Login + " " + PrivacyNotices.Text);
                return;
            }

            try
            {
                RicsApi.Deliver(line);
                Forwarded++;
            }
            catch (Exception ex)
            {
                LastError = (ex.InnerException ?? ex).Message;
                Log.Error(LogPrefix + "Nachricht konnte nicht an RICS gehen: " + (ex.InnerException ?? ex));
            }
        }

        private static void Send(string message)
        {
            if (Settings.testMode || Link == null)
            {
                Log.Message(LogPrefix + "Chat ← " + message);
                return;
            }
            Link.EnqueueSend(message);
        }
    }
}
