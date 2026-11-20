// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ereignis-Abstimmung im Spiel (Stufe 3, Feature-Issue #4).
// Abstimmen lässt bis zum Server-Vertrag (Server-Issue #81) eine Test-Abstimmung im Spiel: Stimmen per Klick, sonst Zufall.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using TwitchCompanion.RimWorld.Core;
using UnityEngine;
using Verse;

namespace TwitchCompanion.RimWorld
{
    internal static class EventVotes
    {
        private const string LogPrefix = "[Twitch Companion] ";
        private static readonly System.Random Rng = new System.Random();

        public static VoteRound Current { get; private set; }
        public static string LastResult { get; private set; }
        private static DateTime? _lastEndUtc;
        private static DateTime? _armedUtc;
        private static string _lastWinner;

        private static CompanionSettings Settings => TwitchCompanionMod.Settings;

        /// <summary>Darf gerade abgestimmt werden? Wie Befehle: Brücke aktiv und Kategorie RimWorld, im Testmodus immer.</summary>
        private static bool Allowed =>
            Settings.eventVotes && Bridge.IsActive && Find.TickManager != null && (Settings.testMode || (Bridge.Link?.GateOpen ?? false));

        /// <summary>Im Hauptthread, aus <see cref="Bridge.MainThreadTick"/>.</summary>
        public static void Tick()
        {
            DateTime now = DateTime.UtcNow;
            VoteRound round = Current;
            if (round != null)
            {
                if (round.IsDue(now)) Finish(round, now);
                return;
            }
            if (!Allowed) { _armedUtc = null; return; }
            _armedUtc ??= now;
            if (VoteSchedule.IsDue(_lastEndUtc, _armedUtc, now, TimeSpan.FromMinutes(Math.Max(1, Settings.eventIntervalMinutes))))
                Start(now);
        }

        /// <summary>Neue Runde mit Vorschlägen des Erzählers; falsch, wenn nicht genug Ereignisse möglich sind.</summary>
        public static bool Start(DateTime nowUtc)
        {
            if (Current != null || global::Verse.Current.Game == null) return false;
            List<EventCandidate> options = EventPlanner.Propose(Candidates(), Settings.eventOptions, Rng, Settings.eventIncludeDoom, _lastWinner);
            if (options.Count < EventPlanner.MinOptions)
            {
                _lastEndUtc = nowUtc; // nicht in jedem Takt neu versuchen
                LastResult = "Keine Abstimmung: zu wenige Ereignisse möglich (" + options.Count + ").";
                Log.Message(LogPrefix + LastResult);
                return false;
            }
            Current = new VoteRound(options, nowUtc, Settings.eventVoteSeconds) { ChannelPointsPerVote = VoteSchedule.ChannelPoints(Rng) };
            Messages.Message("Abstimmung: " + string.Join(" · ", Current.OptionTitles), MessageTypeDefOf.NeutralEvent, false);
            if (Settings.eventOverlay) Find.WindowStack.Add(new EventVoteWindow());
            return true;
        }

        private static void Finish(VoteRound round, DateTime nowUtc)
        {
            EventCandidate winner = round.Resolve(Rng);
            Current = null;
            _lastEndUtc = nowUtc;
            if (winner == null) return;
            _lastWinner = winner.DefName;
            bool fired = Fire(winner.DefName);
            LastResult = DateTime.Now.ToString("HH:mm") + ": „" + winner.Label + "“ (" + round.Votes[round.WinnerIndex] + " Stimmen)"
                + (fired ? string.Empty : " – ließ sich nicht auslösen");
            Messages.Message("Der Chat hat gewählt: " + winner.Label + (fired ? string.Empty : " – geht gerade nicht."),
                fired ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
            Log.Message(LogPrefix + "Ereignis-Abstimmung: " + LastResult);
        }

        /// <summary>Test-Abstimmung sofort auswerten (Knopf im Fenster).</summary>
        public static void FinishNow()
        {
            if (Current != null) Finish(Current, DateTime.UtcNow);
        }

        public static void Cancel()
        {
            if (Current == null) return;
            Current.State = VoteState.Cancelled;
            Current = null;
            _lastEndUtc = DateTime.UtcNow;
        }

        // ── Ereignisse aus RICS ─────────────────────────────────────────

        /// <summary>
        /// Ereignisse aus der RICS-Ereignisliste (<c>IncidentsManager.AllBuyableIncidents</c>), ohne Wetter, die gerade
        /// auf einer Heimatkarte auslösbar sind. Hat der Streamer in RICS Ereignisse eingeschaltet, nur diese; sonst alle,
        /// die RICS für Befehle geeignet hält.
        /// </summary>
        public static List<EventCandidate> Candidates()
        {
            var all = new List<(EventCandidate c, bool enabled)>();
            Type manager = AccessTools.TypeByName("CAP_ChatInteractive.Incidents.IncidentsManager");
            if (!(AccessTools.Property(manager, "AllBuyableIncidents")?.GetValue(null) is IDictionary dict)) return new List<EventCandidate>();
            Map map = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome);
            if (map == null) return new List<EventCandidate>();
            foreach (object inc in dict.Values)
            {
                if (inc == null || Get<bool>(inc, "IsWeatherIncident") || !Get<bool>(inc, "ShouldBeInStore") || !Get<bool>(inc, "IsAvailableForCommands")) continue;
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(Get<string>(inc, "DefName"));
                if (def?.Worker == null || !CanFire(def, map)) continue;
                all.Add((new EventCandidate
                {
                    DefName = def.defName,
                    Label = def.LabelCap.ToString(),
                    Karma = EventCandidate.ParseKarma(Get<string>(inc, "KarmaType")),
                    Weight = def.baseChance,
                }, Get<bool>(inc, "Enabled")));
            }
            var enabled = all.Where(a => a.enabled).Select(a => a.c).ToList();
            return enabled.Count >= Settings.eventOptions ? enabled : all.Select(a => a.c).ToList();
        }

        private static bool CanFire(IncidentDef def, Map map)
        {
            try
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                parms.forced = true;
                return def.Worker.CanFireNow(parms) && !def.Worker.FiredTooRecently(map);
            }
            catch { return false; }
        }

        /// <summary>Löst das Ereignis aus wie RICS (<c>DefaultParmsNow</c>, erzwungen), auf einer zufälligen Heimatkarte.</summary>
        public static bool Fire(string defName)
        {
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            if (def?.Worker == null) return false;
            foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome).InRandomOrder())
            {
                try
                {
                    IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                    parms.forced = true;
                    if (def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms)) return true;
                }
                catch (Exception ex) { Log.Warning(LogPrefix + "Ereignis " + defName + " nicht ausgelöst: " + ex.Message); }
            }
            return false;
        }

        private static T Get<T>(object o, string name)
        {
            object v = AccessTools.Property(o.GetType(), name)?.GetValue(o);
            return v is T t ? t : default;
        }
    }

    /// <summary>Einblendung der laufenden Abstimmung; im Stream sichtbar. Test-Stimmen per Klick.</summary>
    internal sealed class EventVoteWindow : Window
    {
        public EventVoteWindow()
        {
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override Vector2 InitialSize => new Vector2(420f, 90f + 34f * (EventVotes.Current?.Options.Count ?? 3));

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            windowRect.y = 40f;
        }

        public override void DoWindowContents(Rect inRect)
        {
            VoteRound round = EventVotes.Current;
            if (round == null) { Close(); return; }
            TimeSpan left = round.Remaining(DateTime.UtcNow);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), round.Title);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 32f, inRect.width, 24f),
                "Noch " + (int)left.TotalMinutes + ":" + left.Seconds.ToString("00")
                + (round.ChannelPointsPerVote > 0 ? " · Zusatzstimmen: " + round.ChannelPointsPerVote + " Kanalpunkte" : string.Empty)
                + " · Test-Abstimmung");
            float y = 60f;
            for (int i = 0; i < round.Options.Count; i++)
            {
                EventCandidate o = round.Options[i];
                Widgets.Label(new Rect(0f, y, inRect.width - 120f, 30f),
                    (i + 1) + ". " + o.Label + " (" + VoteRound.KarmaWord(o.Karma) + ") – " + round.Votes[i]);
                int index = i;
                if (Widgets.ButtonText(new Rect(inRect.width - 110f, y, 50f, 26f), "+1")) round.AddVote(index);
                if (Widgets.ButtonText(new Rect(inRect.width - 55f, y, 55f, 26f), "+10")) round.AddVote(index, 10);
                y += 34f;
            }
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 26f, 140f, 26f), "Jetzt auswerten")) EventVotes.FinishNow();
            if (Widgets.ButtonText(new Rect(150f, inRect.height - 26f, 120f, 26f), "Abbrechen")) EventVotes.Cancel();
        }
    }
}
