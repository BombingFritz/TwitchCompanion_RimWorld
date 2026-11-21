// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – eingehender Twitch-Raid im Spiel (Stufe 2, Feature-Issue #3; Server-Spec chat-bruecke.md, Teil D).
// Vorerst nur die Rolle „Angriff“ über den RICS-Raid mit Beitrittsfenster; Rollen und Gegenteil-Regel folgen.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using TwitchCompanion.RimWorld.Core;
using Verse;

namespace TwitchCompanion.RimWorld
{
    internal static class Raids
    {
        private const string LogPrefix = "[Twitch Companion] ";
        /// <summary>Wer in dieser Zeit vor dem Raid geplaudert hat, tritt bei (Vorschlag von Claude).</summary>
        public static readonly TimeSpan AutoJoinWindow = TimeSpan.FromMinutes(10);

        public static string LastRaid { get; private set; }

        private static CompanionSettings Settings => TwitchCompanionMod.Settings;

        /// <summary>Im Hauptthread. Nur in der Kategorie RimWorld und mit geladenem Spielstand; sonst verfällt der Raid (Server liefert nicht nach).</summary>
        public static void OnRaid(RaidInfo raid, List<ActiveChatter> chatters)
        {
            if (raid == null) return;
            string who = raid.NameForGame;
            if (!Settings.twitchRaids) { Note("Raid von " + who + " (" + raid.Viewers + "): im Spiel ausgeschaltet."); return; }
            if (!Bridge.IsActive || !(Bridge.Link?.GateOpen ?? false)) { Note("Raid von " + who + ": nicht in der Kategorie RimWorld, nichts im Spiel."); return; }
            if (Current.Game == null || Find.CurrentMap == null) { Note("Raid von " + who + ": kein Spielstand geladen."); return; }

            object service = TwitchService;
            if (service == null) { Note("Raid von " + who + ": RICS-TwitchService nicht gefunden."); return; }
            if (!RicsRaidsEnabled)
            {
                Note("Raid von " + who + ": In RICS sind Twitch-Raids ausgeschaltet – nichts im Spiel.");
                Messages.Message("Twitch-Raid von " + who + ": In den RICS-Einstellungen sind Twitch-Raids aus.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            try
            {
                AccessTools.Method(service.GetType(), "StartRaidJoinCollection", new[] { typeof(string), typeof(int) })
                    .Invoke(service, new object[] { who, Math.Max(1, raid.Viewers) });
                int joined = 0;
                if (Settings.raidAutoJoinActive)
                {
                    var join = AccessTools.Method(service.GetType(), "ProcessUserJoined", new[] { typeof(string) });
                    foreach (string name in RaidJoiners.Select(chatters, raid, DateTime.UtcNow, AutoJoinWindow))
                    {
                        join?.Invoke(service, new object[] { name });
                        joined++;
                    }
                }
                Note("Raid von " + who + " mit " + raid.Viewers + " Zuschauern: Beitrittsfenster offen (" + joined + " Plaudernde automatisch dabei, sonst !joinraid).");
            }
            catch (Exception ex)
            {
                Log.Error(LogPrefix + "Raid von " + who + " nicht gestartet: " + (ex.InnerException ?? ex));
            }
        }

        // ── P15: Stärke des Twitch-Raids begrenzen (Issue #31) ────────────

        private static readonly System.Random Rng = new System.Random();

        public static string Apply(Harmony harmony)
        {
            MethodInfo worker = AccessTools.Method(AccessTools.TypeByName("CAP_ChatInteractive.Incidents.IncidentWorker_TwitchRaid"), "TryExecuteWorker");
            if (worker == null) return "P15 fehlt";
            harmony.Patch(worker, prefix: new HarmonyMethod(typeof(Raids), nameof(WorkerPrefix)));
            return "P15";
        }

        /// <summary>Vor dem Erzeugen der Raider: Namensliste auf so viele kürzen, wie die Bedrohungspunkte tragen.</summary>
        private static void WorkerPrefix(IncidentParms parms)
        {
            try
            {
                if (!(AccessTools.Property(AccessTools.TypeByName("CAP_ChatInteractive.Incidents.IncidentWorker_TwitchRaid"), "CurrentRaidUsernames")?.GetValue(null) is List<string> names)) return;
                int before = names.Count(n => !string.IsNullOrWhiteSpace(n));
                int max = RaidJoiners.MaxFighters(parms?.points ?? 0f, Settings.raidStrengthFactor, before);
                if (before <= max) return;
                List<string> kept = RaidJoiners.Trim(names, max, Rng);
                names.Clear();
                names.AddRange(kept);
                Note("Raid begrenzt: " + max + " von " + before + " Raidern kämpfen (Bedrohung " + (int)(parms?.points ?? 0f) + ", Faktor " + Settings.raidStrengthFactor.ToString("0.0#") + ").");
            }
            catch (Exception ex) { Log.Warning(LogPrefix + "P15: " + ex.Message); }
        }

        /// <summary>Einmal nach dem Laden: Warnung, wenn Raids an sind, RICS sie aber ausgeschaltet hat (Issue #30).</summary>
        public static void CheckSettingsOnLoad()
        {
            if (!Settings.twitchRaids || RicsRaidsEnabled) return;
            Log.Warning(LogPrefix + "In RICS sind Twitch-Raids ausgeschaltet: Eingehende Raids kommen nicht im Spiel an. In RICS einschalten und das RICS-Fenster schließen, damit RICS die Einstellung speichert.");
            Messages.Message("Twitch Companion: In RICS sind Twitch-Raids aus – Raids kommen nicht im Spiel an.", MessageTypeDefOf.CautionInput, false);
        }

        private static object TwitchService
        {
            get
            {
                Type mod = AccessTools.TypeByName("CAP_ChatInteractive.CAPChatInteractiveMod");
                object instance = mod == null ? null : AccessTools.Property(mod, "Instance")?.GetValue(null);
                return instance == null ? null : AccessTools.Property(mod, "TwitchService")?.GetValue(instance);
            }
        }

        /// <summary>RICS-Schalter „Twitch-Raids“ (<c>GlobalSettings.TwitchRaidsEnabled</c>, Vorgabe aus); ohne ihn startet RICS den Raid nach dem Fenster nicht.</summary>
        public static bool RicsRaidsEnabled
        {
            get
            {
                object settings = Rics.RicsApi.GetGlobalSettings();
                if (settings == null) return false;
                object v = AccessTools.Field(settings.GetType(), "TwitchRaidsEnabled")?.GetValue(settings);
                return v is bool b && b;
            }
        }

        private static void Note(string text)
        {
            LastRaid = DateTime.Now.ToString("HH:mm") + ": " + text;
            Log.Message(LogPrefix + text);
        }
    }
}
