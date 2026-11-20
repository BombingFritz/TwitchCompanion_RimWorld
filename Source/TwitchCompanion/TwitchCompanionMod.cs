// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Mod-Einstieg, Einstellungen, Hauptthread-Takt.

using System;
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

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref testMode, "testMode", false);
            Scribe_Values.Look(ref requireCategory, "requireCategory", true);
            Scribe_Values.Look(ref serverUrl, "serverUrl", DefaultServerUrl);
        }
    }

    public class TwitchCompanionMod : Mod
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

        private void DrawSettings(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);

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
            if (Current.ProgramState != ProgramState.Playing)
                list.Label("  Spiel: kein Spielstand geladen – Befehle aus dem Chat werden erst in einer geladenen Kolonie verarbeitet, ältere als 60 s verfallen.");
            if (!string.IsNullOrEmpty(Bridge.LastError)) list.Label("  Letzter Fehler: " + Bridge.LastError);
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

            list.End();
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
                RicsPatches.Apply(new Harmony("BombingFritz.TwitchCompanion.RimWorld"));
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
