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
        public bool enabled = true;
        public bool testMode;
        public string serverUrl = "http://localhost:3000";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref testMode, "testMode", false);
            Scribe_Values.Look(ref serverUrl, "serverUrl", "http://localhost:3000");
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
            var list = new Listing_Standard();
            list.Begin(inRect);

            list.CheckboxLabeled("Brücke aktiv", ref Settings.enabled,
                "RICS bekommt Befehle über den Server und antwortet über ihn. In RICS keinen Twitch-Token eintragen.");
            list.Label("Server-Adresse (Anbindung folgt, sobald die Schnittstelle vereinbart ist):");
            Settings.serverUrl = list.TextEntry(Settings.serverUrl);
            list.Gap();

            list.Label("Status");
            list.Label("  RICS: " + (RicsApi.Available ? "gefunden" : "nicht nutzbar – " + RicsApi.Problem));
            list.Label("  Verbindung: " + (Settings.testMode ? "Testmodus (ohne Server)" : "Server-Anbindung noch nicht enthalten"));
            list.Label("  Brücke: " + (Bridge.IsActive ? "aktiv" : "aus"));
            if (!string.IsNullOrEmpty(Bridge.LastError)) list.Label("  Letzter Fehler: " + Bridge.LastError);
            list.GapLine();

            list.CheckboxLabeled("Testmodus (ohne Server)", ref Settings.testMode,
                "Antworten von RICS gehen ins Log statt in den Chat. Testnachrichten können unten eingespeist werden.");
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
            }
            catch (Exception ex)
            {
                Log.Error("[Twitch Companion] Patches an RICS fehlgeschlagen: " + ex);
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
