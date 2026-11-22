// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Mod-Einstieg, Einstellungen, Hauptthread-Takt.

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TwitchCompanion.RimWorld.Core;
using TwitchCompanion.RimWorld.Rics;
using UnityEngine;
using Verse;

namespace TwitchCompanion.RimWorld
{
    public class CompanionSettings : ModSettings
    {
        public const string DefaultServerUrl = "http://localhost:3000";

        public bool enabled = true;
        public bool testMode;
        public bool requireCategory = true;
        public string serverUrl = DefaultServerUrl;
        public bool lootboxOdds = true;
        public bool betBonus = true;
        public bool germanChat = true;
        public bool repliesInGame = true;
        public bool autoLootbox = true;
        public string allowedCommands = CommandFilter.DefaultAllowed;
        public int pawnCooldownMinutes = 30;
        public long lastPawnPurchaseTicks;

        public DateTime? LastPawnPurchaseUtc => lastPawnPurchaseTicks > 0 ? new DateTime(lastPawnPurchaseTicks, DateTimeKind.Utc) : (DateTime?)null;

        public void MarkPawnPurchase(DateTime utc)
        {
            lastPawnPurchaseTicks = utc.Ticks;
            Write();
        }

        /// <summary>Twitch-IDs (keine Namen) aller, die sich in diesem Stream angestellt haben; geleert nach dem Stream.</summary>
        public List<string> queueJoinedThisStream = new List<string>();
        /// <summary>Verwarnungen wegen Spam im laufenden Stream, „userId=Anzahl“ (keine Namen).</summary>
        public List<string> spamStrikes = new List<string>();
        /// <summary>Was mit einem Kolonisten nach <c>!leave</c> passiert; später per Abstimmung (blockiert durch Server-Issue #81).</summary>
        public LeaveMode leaveMode = LeaveMode.StaysAsDulli;

        // Ereignis-Abstimmung (Stufe 3, Feature-Issue #4); Startwerte Vorschlag von Claude, OFFEN.md #13.
        public bool eventVotes;
        public int eventGraceMinutes = 60;
        public int eventIntervalMinutes = 30;
        public int eventQuietMinutes = 10;
        public int eventVoteSeconds = 120;
        public int eventOptions = 4;
        public bool eventOverlay = true;
        /// <summary>Echte Twitch-Abstimmung über den Server (Server-PR #83); aus = Test-Abstimmung im Spiel.</summary>
        public bool eventTwitchPoll = true;
        /// <summary>Eingehende Twitch-Raids im Spiel (RICS-Raid mit Beitrittsfenster, Server-PR #83).</summary>
        public bool twitchRaids = true;
        /// <summary>Wer in den letzten Minuten vor dem Raid geplaudert hat, tritt dem Raid automatisch bei.</summary>
        public bool raidAutoJoinActive = true;
        /// <summary>Stärke des Twitch-Raids: kämpfende Raider = Bedrohungspunkte × Faktor / 45 (Issue #31).</summary>
        public float raidStrengthFactor = 1f;

        // Killer-Kaninchen (Nutzer, 22.11.2026); Fenster, Abstand und Stärke Vorschlag von Claude.
        public bool rabbitsEnabled = true;
        public float rabbitPriceShare = 0.5f;
        public int rabbitWindowSeconds = 90;
        public int rabbitCooldownMinutes = 20;
        public float rabbitStrengthFactor = 1f;
        public long lastRabbitTicks;

        public DateTime? LastRabbitUtc => lastRabbitTicks > 0 ? new DateTime(lastRabbitTicks, DateTimeKind.Utc) : (DateTime?)null;

        public void MarkRabbit(DateTime utc)
        {
            lastRabbitTicks = utc.Ticks;
            Write();
        }
        /// <summary>In diesem Stream schon gewonnene Ereignisse (DefNames, keine Personendaten).</summary>
        public List<string> eventsUsedThisStream = new List<string>();
        /// <summary>Beginn des laufenden Streams laut Mod (UTC-Ticks), 0 = unbekannt.</summary>
        public long streamStartedTicks;

        public DateTime? StreamStartedUtc => streamStartedTicks > 0 ? new DateTime(streamStartedTicks, DateTimeKind.Utc) : (DateTime?)null;

        public void MarkEventUsed(string defName)
        {
            eventsUsedThisStream ??= new List<string>();
            if (!eventsUsedThisStream.Contains(defName)) eventsUsedThisStream.Add(defName);
            Write();
        }
        /// <summary>Zuletzt gesehen, dass der Stream live ist (UTC-Ticks), 0 = unbekannt.</summary>
        public long lastLiveTicks;

        public DateTime? LastLiveUtc
        {
            get => lastLiveTicks > 0 ? new DateTime(lastLiveTicks, DateTimeKind.Utc) : (DateTime?)null;
            set => lastLiveTicks = value?.Ticks ?? 0;
        }

        public bool HasJoinedQueue(string userId) => !string.IsNullOrEmpty(userId) && queueJoinedThisStream != null && queueJoinedThisStream.Contains(userId);

        public void MarkJoinedQueue(string userId)
        {
            if (string.IsNullOrEmpty(userId) || HasJoinedQueue(userId)) return;
            queueJoinedThisStream ??= new List<string>();
            queueJoinedThisStream.Add(userId);
            Write();
        }
        public float priceFactor = 10f;
        /// <summary>IDs bereits belohnter Wetten (keine Personendaten), höchstens 50.</summary>
        public List<string> awardedPredictions = new List<string>();

        /// <summary>Wahr, wenn die Wette noch nicht belohnt war; merkt sie sich und speichert.</summary>
        public bool MarkPredictionAwarded(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            awardedPredictions ??= new List<string>();
            if (awardedPredictions.Contains(id)) return false;
            awardedPredictions.Add(id);
            while (awardedPredictions.Count > 50) awardedPredictions.RemoveAt(0);
            Write();
            return true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref testMode, "testMode", false);
            Scribe_Values.Look(ref requireCategory, "requireCategory", true);
            Scribe_Values.Look(ref serverUrl, "serverUrl", DefaultServerUrl);
            Scribe_Values.Look(ref lootboxOdds, "lootboxOdds", true);
            Scribe_Values.Look(ref betBonus, "betBonus", true);
            Scribe_Values.Look(ref germanChat, "germanChat", true);
            Scribe_Values.Look(ref repliesInGame, "repliesInGame", true);
            Scribe_Values.Look(ref autoLootbox, "autoLootbox", true);
            Scribe_Values.Look(ref allowedCommands, "allowedCommands", CommandFilter.DefaultAllowed);
            Scribe_Values.Look(ref pawnCooldownMinutes, "pawnCooldownMinutes", 30);
            Scribe_Values.Look(ref lastPawnPurchaseTicks, "lastPawnPurchaseTicks", 0L);
            Scribe_Values.Look(ref priceFactor, "priceFactor", 10f);
            Scribe_Collections.Look(ref awardedPredictions, "awardedPredictions", LookMode.Value);
            Scribe_Collections.Look(ref queueJoinedThisStream, "queueJoinedThisStream", LookMode.Value);
            Scribe_Values.Look(ref lastLiveTicks, "lastLiveTicks", 0L);
            Scribe_Collections.Look(ref spamStrikes, "spamStrikes", LookMode.Value);
            Scribe_Values.Look(ref leaveMode, "leaveMode", LeaveMode.StaysAsDulli);
            Scribe_Values.Look(ref eventVotes, "eventVotes", false);
            Scribe_Values.Look(ref eventGraceMinutes, "eventGraceMinutes", 60);
            Scribe_Values.Look(ref eventIntervalMinutes, "eventIntervalMinutes2", 30); // neuer Schlüssel: Vorgabe 20 → 30
            Scribe_Values.Look(ref eventQuietMinutes, "eventQuietMinutes", 10);
            Scribe_Values.Look(ref eventVoteSeconds, "eventVoteSeconds", 120);
            Scribe_Values.Look(ref eventOptions, "eventOptions2", 4); // neuer Schlüssel: Vorgabe 3 → 4
            Scribe_Collections.Look(ref eventsUsedThisStream, "eventsUsedThisStream", LookMode.Value);
            Scribe_Values.Look(ref streamStartedTicks, "streamStartedTicks", 0L);
            Scribe_Values.Look(ref eventOverlay, "eventOverlay", true);
            Scribe_Values.Look(ref eventTwitchPoll, "eventTwitchPoll", true);
            Scribe_Values.Look(ref twitchRaids, "twitchRaids", true);
            Scribe_Values.Look(ref raidAutoJoinActive, "raidAutoJoinActive", true);
            Scribe_Values.Look(ref raidStrengthFactor, "raidStrengthFactor", 1f);
            Scribe_Values.Look(ref rabbitsEnabled, "rabbitsEnabled", true);
            Scribe_Values.Look(ref rabbitPriceShare, "rabbitPriceShare", 0.5f);
            Scribe_Values.Look(ref rabbitWindowSeconds, "rabbitWindowSeconds", 90);
            Scribe_Values.Look(ref rabbitCooldownMinutes, "rabbitCooldownMinutes", 20);
            Scribe_Values.Look(ref rabbitStrengthFactor, "rabbitStrengthFactor", 1f);
            Scribe_Values.Look(ref lastRabbitTicks, "lastRabbitTicks", 0L);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                queueJoinedThisStream ??= new List<string>();
                spamStrikes ??= new List<string>();
                eventsUsedThisStream ??= new List<string>();
                allowedCommands = CommandFilter.Migrate(allowedCommands);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && awardedPredictions == null) awardedPredictions = new List<string>();
        }
    }

    public partial class TwitchCompanionMod : Mod
    {
        public static CompanionSettings Settings { get; private set; }

        // Felder für die Testnachricht (nicht gespeichert)
        private string _testName = "testzuschauer";
        private string _testText = "!bal";
        private bool _testMod, _testVip, _testSub, _testPrivate;

        public TwitchCompanionMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CompanionSettings>();
        }

        public override string SettingsCategory() => "Twitch Companion";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            string before = Settings.enabled + "|" + Settings.testMode + "|" + Settings.serverUrl;
            DrawSettings(inRect);
            // Geänderte Verbindungseinstellungen sofort wirksam machen, nicht erst mit der nächsten Zeile des Servers.
            if (before != Settings.enabled + "|" + Settings.testMode + "|" + Settings.serverUrl) Bridge.Link?.Reconnect();
        }

        private Vector2 _scroll;
        private float _viewHeight = 1600f;

        private void DrawSettings(Rect inRect)
        {
            var view = new Rect(0f, 0f, inRect.width - 20f, _viewHeight);
            Widgets.BeginScrollView(inRect, ref _scroll, view);
            var list = new Listing_Standard();
            list.Begin(view);

            list.CheckboxLabeled("Brücke aktiv", ref Settings.enabled,
                "RICS bekommt Befehle über den Server und antwortet über ihn. In RICS keinen Twitch-Token eintragen.");
            list.Label("Server-Adresse:");
            Settings.serverUrl = list.TextEntry(Settings.serverUrl);
            if (Bridge.ServerUrlOverridden) list.Label("  (gerade überschrieben durch die Befehlszeile: " + Bridge.ServerUrl + ")");
            list.CheckboxLabeled("Nur in der Twitch-Kategorie RimWorld", ref Settings.requireCategory,
                "Befehle, Käufe und Coins nur, solange der Server die Kategorie RimWorld meldet. Ausschalten nur, wenn der Server die Kategorie noch nicht liefern kann.");
            list.Gap();

            ServerLink link = Bridge.Link;
            list.Label("Status");
            list.Label("  RICS: " + (RicsApi.Available ? "gefunden" : "nicht nutzbar – " + RicsApi.Problem));
            if (Settings.testMode) list.Label("  Verbindung: Testmodus (ohne Server)");
            else if (link == null) list.Label("  Verbindung: nicht gestartet");
            else if (link.Connected) list.Label("  Verbindung: verbunden seit " + link.ConnectedSinceUtc?.ToLocalTime().ToString("HH:mm:ss"));
            else list.Label("  Verbindung: nicht verbunden" + (link.LastError != null ? " – " + link.LastError : string.Empty));
            if (link != null && !Settings.testMode)
            {
                ChannelInfo ch = link.Channel;
                string cat = ch != null ? "„" + (ch.Category ?? "unbekannt") + "“" + (ch.Live ? ", live" : ", nicht live") : (link.ChannelProblem ?? "unbekannt");
                list.Label("  Kategorie: " + cat + (link.GateOpen ? " → Befehle und Coins an" : " → Befehle und Coins aus"));
                list.Label("  Weitergeleitet: " + Bridge.Forwarded + ", wegen Kategorie verworfen: " + Bridge.BlockedByCategory
                    + ", !privat: " + Bridge.PrivateIgnored);
                list.Label("  Gesendet: " + link.SentCount + ", wartend: " + link.PendingSends + ", verworfen: " + link.DroppedSendCount);
                if (link.LastSendError != null) list.Label("  Letzter Sendefehler: " + link.LastSendError);
                if (Bridge.LastActivitySummary != null) list.Label("  Coins: " + Bridge.LastActivitySummary);
            }
            list.Label("  Brücke: " + (Bridge.IsActive ? "aktiv" : "aus"));
            if (!string.IsNullOrEmpty(Bridge.LastError)) list.Label("  Letzter Fehler: " + Bridge.LastError);
            list.GapLine();

            DrawEconomy(list);
            list.GapLine();
            DrawEvents(list);
            list.GapLine();

            list.CheckboxLabeled("Testmodus (ohne Server)", ref Settings.testMode,
                "Keine Serververbindung. Antworten von RICS gehen ins Log statt in den Chat. Testnachrichten können unten eingespeist werden.");
            if (Settings.testMode)
            {
                list.Label("Testnachricht einspeisen (läuft wie eine Chatnachricht durch RICS):");
                _testName = list.TextEntryLabeled("Login", _testName);
                _testText = list.TextEntryLabeled("Text", _testText);
                list.CheckboxLabeled("Moderator", ref _testMod);
                list.CheckboxLabeled("VIP", ref _testVip);
                list.CheckboxLabeled("Subscriber", ref _testSub);
                list.CheckboxLabeled("!privat", ref _testPrivate);
                bool inGame = Current.Game != null;
                if (!inGame) list.Label("Erst einen Spielstand laden.");
                if (list.ButtonText("Einspeisen") && inGame)
                {
                    string login = (_testName ?? string.Empty).Trim().ToLowerInvariant();
                    Bridge.EnqueueIncoming(new ChatLine
                    {
                        MessageId = Guid.NewGuid().ToString("N"),
                        UserId = "test-" + login,
                        Login = login,
                        DisplayName = _testName?.Trim(),
                        Text = _testText,
                        IsModerator = _testMod,
                        IsVip = _testVip,
                        IsSubscriber = _testSub,
                        IsPrivate = _testPrivate,
                    });
                    Messages.Message("Testnachricht eingespeist; Antwort im Log.", MessageTypeDefOf.NeutralEvent, false);
                }
            }

            _viewHeight = Math.Max(inRect.height, list.CurHeight + 40f);
            list.End();
            Widgets.EndScrollView();
        }

        private string _intervalBuffer, _voteSecondsBuffer, _optionsBuffer, _graceBuffer, _quietBuffer;
        private bool _showCatalog;
        private string _rabbitWindowBuffer, _rabbitCooldownBuffer;

        /// <summary>Ereignis-Abstimmung: Der Erzähler schlägt vor, der Chat wählt (Stufe 3, Feature-Issue #4).</summary>
        private void DrawEvents(Listing_Standard list)
        {
            list.Label("Ereignisse");
            list.CheckboxLabeled("Ereignis-Abstimmungen", ref Settings.eventVotes,
                "Der Erzähler schlägt Ereignisse aus der Liste der Mod vor, der Chat wählt, der Gewinner wird ausgelöst. "
                + "Abgestimmt wird über eine echte Twitch-Abstimmung, die der Server startet; im Testmodus über eine Test-Abstimmung im Spiel (Stimmen per Klick, sonst Zufall).");
            list.CheckboxLabeled("  Über Twitch abstimmen (Server)", ref Settings.eventTwitchPoll,
                "Aus: Test-Abstimmung im Spiel, auch mit Server. Der Server braucht den Scope channel:manage:polls und eine neue Anmeldung (/login).");
            list.TextFieldNumericLabeled("  Gnadenfrist ab Streambeginn (min)", ref Settings.eventGraceMinutes, ref _graceBuffer, 0, 600);
            list.TextFieldNumericLabeled("  Abstand (min)", ref Settings.eventIntervalMinutes, ref _intervalBuffer, 1, 240);
            list.TextFieldNumericLabeled("  Ruhe nach Ereignis des Spiel-Erzählers (min)", ref Settings.eventQuietMinutes, ref _quietBuffer, 0, 120);
            list.TextFieldNumericLabeled("  Dauer der Abstimmung (s)", ref Settings.eventVoteSeconds, ref _voteSecondsBuffer, VoteRound.MinSeconds, VoteRound.MaxSeconds);
            list.TextFieldNumericLabeled("  Optionen", ref Settings.eventOptions, ref _optionsBuffer, EventPlanner.MinOptions, EventPlanner.MaxOptions);
            list.Label("  Je Runde: ein gutes, ein schlechtes, „Nichts passiert“ oder „Wetterwechsel“, dann neutrale. Nichts Verheerendes, nichts extrem Gutes; Gewinner kommen im selben Stream nicht wieder"
                + " (bisher " + (Settings.eventsUsedThisStream?.Count ?? 0) + ").");
            if (list.ButtonText(_showCatalog ? "  Ereignisliste ausblenden" : "  Ereignisliste zeigen (" + EventCatalog.Default.Count + ")")) _showCatalog = !_showCatalog;
            if (_showCatalog)
            {
                if (Current.Game == null) list.Label("    Erst einen Spielstand laden.");
                else foreach (string line in EventVotes.CatalogStatus()) list.Label("    " + line);
            }
            if (EventVotes.Pending != null) list.Label("  Wartet: " + EventVotes.Pending.Label + " (Ruhe nach Ereignis des Spiel-Erzählers)");
            list.CheckboxLabeled("  Einblendung im Spiel", ref Settings.eventOverlay, "Fenster oben mit Optionen und Restzeit; im Test mit Knöpfen für Stimmen.");
            if (EventVotes.Current != null) list.Label("  Läuft: " + string.Join(" · ", EventVotes.Current.OptionTitles));
            list.GapLine();
            list.Label("Twitch-Raids");
            list.CheckboxLabeled("Eingehende Raids im Spiel", ref Settings.twitchRaids,
                "Der Server meldet einen Raid, die Mod öffnet das RICS-Beitrittsfenster (!joinraid). Der Anführer heißt wie der raidende Kanal. Vorerst immer als Angriff; Rollen folgen.");
            list.CheckboxLabeled("  Plaudernde der letzten " + (int)Raids.AutoJoinWindow.TotalMinutes + " min treten automatisch bei", ref Settings.raidAutoJoinActive);
            list.Label("  Stärke: so viele Kämpfer, wie ein normaler Raid gleicher Bedrohung hätte, mal " + Settings.raidStrengthFactor.ToString("0.0#")
                + " (mindestens einer; der raidende Kanal ist immer dabei, die übrigen werden ausgelost)");
            Settings.raidStrengthFactor = (float)Math.Round(list.Slider(Settings.raidStrengthFactor, 0.5f, 3f) * 4f) / 4f;
            list.GapLine();
            list.Label("Killer-Kaninchen ($Killer-Kaninchen, Mitmachen mit !joinraid)");
            list.CheckboxLabeled("  kaufbar", ref Settings.rabbitsEnabled);
            list.Label("  Preis: " + (int)Math.Round(Settings.rabbitPriceShare * 100) + " % eines neuen Kolonisten = " + (Rabbits.Price?.ToString() ?? "?") + " Pfennig");
            Settings.rabbitPriceShare = (float)Math.Round(list.Slider(Settings.rabbitPriceShare, 0.1f, 2f) * 20f) / 20f;
            list.TextFieldNumericLabeled("  Beitrittsfenster (s)", ref Settings.rabbitWindowSeconds, ref _rabbitWindowBuffer, 15, 600);
            list.TextFieldNumericLabeled("  Höchstens alle … min", ref Settings.rabbitCooldownMinutes, ref _rabbitCooldownBuffer, 0, 600);
            list.Label("  Stärke: so viele Kaninchen, wie die Bedrohung trägt, mal " + Settings.rabbitStrengthFactor.ToString("0.0#") + " (der Käufer ist immer dabei)");
            Settings.rabbitStrengthFactor = (float)Math.Round(list.Slider(Settings.rabbitStrengthFactor, 0.5f, 3f) * 4f) / 4f;
            if (Rabbits.LastAttack != null) list.Label("  Zuletzt: " + Rabbits.LastAttack);
            if (RicsApi.Available && !Raids.RicsRaidsEnabled) list.Label("  ⚠ In RICS sind Twitch-Raids aus: dort „Twitch-Raids“ einschalten, sonst passiert im Spiel nichts.");
            if (Raids.LastRaid != null) list.Label("  Zuletzt: " + Raids.LastRaid);
            if (EventVotes.LastResult != null) list.Label("  Zuletzt: " + EventVotes.LastResult);
            bool inGame = Current.Game != null;
            if (list.ButtonText(!inGame ? "Abstimmung: erst einen Spielstand laden" : EventVotes.UseTwitch ? "Twitch-Abstimmung jetzt starten" : "Test-Abstimmung jetzt starten") && inGame && EventVotes.Current == null)
            {
                if (!EventVotes.Start(DateTime.UtcNow)) Messages.Message(EventVotes.LastResult ?? "Keine Abstimmung möglich.", MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    public partial class TwitchCompanionMod
    {
        private string _factorBuffer;
        private string _cooldownBuffer;

        /// <summary>Wirtschaft: Zuschaustunde, Preis-Werkzeug, Lootbox-Chancen, Wettbonus (Feature-Issues #10, #11).</summary>
        private void DrawEconomy(Listing_Standard list)
        {
            list.Label("Wirtschaft");
            if (!RicsApi.Available) { list.Label("  RICS nicht verfügbar."); return; }
            int perHour = RicsEconomy.CoinsPerHour;
            list.Label("  1 Zuschaustunde = " + perHour + " " + (RicsEconomy.CurrencyName?.Trim() ?? "Coins")
                + " (RICS: " + RicsEconomy.BaseCoinReward + " alle 2 min für aktive Zuschauer, ohne Boni)");
            if (!RicsEconomy.CurrencyIsPfennig && list.ButtonText("Währung in RICS auf „Pfennig“ umstellen"))
            {
                try { RicsEconomy.SetCurrencyPfennig(); Messages.Message("Währung heißt jetzt Pfennig.", MessageTypeDefOf.PositiveEvent, false); }
                catch (Exception ex) { Log.Error("[Twitch Companion] Währung nicht geändert: " + ex); }
            }

            list.TextFieldNumericLabeled("  Item-Faktor auf den RICS-Standardpreis", ref Settings.priceFactor, ref _factorBuffer, 1f, 100f);
            var plan = new PricePlan { ItemFactor = Settings.priceFactor };
            list.Label("  Plan: Items = Standard × " + plan.ItemFactor.ToString("0.#") + ", Tiere mindestens " + Economy.HoursToCoins(plan.AnimalMinHours, perHour)
                + ", neuer Kolonist " + Economy.HoursToCoins(plan.NewPawnHours, perHour) + ", Spargel " + Economy.HoursToCoins(plan.AsparagusMinutes / 60f, perHour) + " je Stück.");
            if (list.ButtonText("Preise nach Zuschauzeit setzen …"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Alle Item-, Tier- und Kolonistenpreise in RICS werden nach dem Plan neu gesetzt und von RICS gespeichert. Eigene Preisänderungen in RICS gehen dabei verloren. Fortfahren?",
                    () =>
                    {
                        var result = RicsEconomy.ApplyPricePlan(plan);
                        Log.Message("[Twitch Companion] Preise gesetzt: " + result);
                        Messages.Message(result.Problem == null ? "Preise gesetzt: " + result : "Preise nicht gesetzt: " + result.Problem,
                            result.Problem == null ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput, false);
                    }, destructive: true));
            }

            list.CheckboxLabeled("Lootbox: über " + LootboxOdds.Threshold + " nur 1 zu 20", ref Settings.lootboxOdds,
                "Sonst " + LootboxOdds.LowMin + "–" + LootboxOdds.Threshold + "; mit 5 % bis " + RicsEconomy.LootboxMax
                + " (Höchstwert aus RICS). Im Mittel etwa " + LootboxOdds.Expected(RicsEconomy.LootboxMax).ToString("0") + " je Lootbox.");
            list.GapLine();
            list.Label("Ausmisten");
            list.CheckboxLabeled("Antworten nur im Spiel (nicht im Chat)", ref Settings.repliesInGame,
                "RICS-Antworten erscheinen als Einblendung in RimWorld und sind im Stream zu sehen; der Chat bleibt frei.");
            list.CheckboxLabeled("Lootbox beim ersten Befehl automatisch öffnen", ref Settings.autoLootbox,
                "Vor dem Befehl werden offene Lootboxen geöffnet; danach steht das Guthaben in der Antwort.");
            list.Label("  Erlaubte Befehle für Zuschauer (Moderatoren und Streamer: alle; „buy“ gilt auch für $-Käufe):");
            Settings.allowedCommands = list.TextEntry(Settings.allowedCommands);
            if (list.ButtonText("  Vorgabe wiederherstellen")) Settings.allowedCommands = CommandFilter.DefaultAllowed;
            list.TextFieldNumericLabeled("  Neuer Kolonist höchstens alle … min (alle zusammen)", ref Settings.pawnCooldownMinutes, ref _cooldownBuffer, 0, 600);
            list.Label("  Warteschlange: Neue stehen vorne, anstellen einmal je Stream, geleert " + (int)QueueRules.StreamGap.TotalMinutes
                + " min nach Streamende. Angestellt in diesem Stream: " + (Settings.queueJoinedThisStream?.Count ?? 0) + ".");
            if (list.ButtonTextLabeled("  Nach !leave", Settings.leaveMode == LeaveMode.StaysAsDulli ? "bleibt als „Dulli“" : "verlässt die Kolonie"))
                Settings.leaveMode = Settings.leaveMode == LeaveMode.StaysAsDulli ? LeaveMode.Departs : LeaveMode.StaysAsDulli;
            list.Label("  Spamschutz: über 5 Befehle je Minute oder über 10 je 10 min = Verwarnung, nach 3 bis Streamende ignoriert. Ignoriert: "
                + (Settings.spamStrikes?.Count(e => e.EndsWith("=3")) ?? 0) + ".");
            list.Label("  Laden: " + string.Join(", ", ShopSelection.Default.Select(e => e.CustomName + " (max. " + e.MaxPerPurchase + ")")));
            if (list.ButtonText("Laden auf diese Auswahl beschränken …"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Alle anderen Items in RICS werden abgeschaltet; die ausgewählten bekommen ihren Namen und eine Grenze je Kauf. Fortfahren?",
                    () =>
                    {
                        string r = RicsEconomy.ApplyShopSelection(ShopSelection.Default);
                        Log.Message("[Twitch Companion] Laden: " + r);
                        Messages.Message("Laden: " + r, r == RicsEconomy.EmptyStore ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.PositiveEvent, false);
                    }, destructive: true));
            }
            list.CheckboxLabeled("Chat kurz und auf Deutsch", ref Settings.germanChat,
                "Kurze !bal-Antwort und deutsche Fassung der fest eingebauten RICS-Texte. Die übrigen RICS-Texte kommen aus der deutschen Sprachdatei dieser Mod, wenn RimWorld auf Deutsch läuft.");
            list.CheckboxLabeled("Wettbonus: 1000 gewonnene Kanalpunkte = 1 Zuschaustunde", ref Settings.betBonus,
                "Ganze Auszahlung zählt, Verlierer verlieren nichts. Twitch meldet nur die bis zu 10 Tipper mit dem höchsten Einsatz je Ausgang.");
        }
    }

    [StaticConstructorOnStartup]
    internal static class Startup
    {
        static Startup()
        {
            RicsApi.Resolve();
            if (!RicsApi.Available) return;
            try
            {
                var harmony = new Harmony("BombingFritz.TwitchCompanion.RimWorld");
                RicsPatches.Apply(harmony);
                IncidentWatch.Apply(harmony); // P13
                Log.Message("[Twitch Companion] Raid: " + Raids.Apply(harmony) + ".");
                Bridge.Start();
            }
            catch (Exception ex)
            {
                Log.Error("[Twitch Companion] Start fehlgeschlagen: " + ex);
            }
        }
    }

    /// <summary>Hauptthread-Takt, läuft jedes Bild, auch pausiert.</summary>
    public class CompanionGameComponent : GameComponent
    {
        public CompanionGameComponent(Game game) { }

        public override void GameComponentUpdate() => Bridge.MainThreadTick();
    }
}
