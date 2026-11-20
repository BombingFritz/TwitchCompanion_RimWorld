// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ereignis-Abstimmung im Spiel (Stufe 3, Feature-Issue #4).
// Abgestimmt wird über eine echte Twitch-Abstimmung, die der Server startet (Server-Spec chat-bruecke.md, Teil C, Server-PR #83);
// im Testmodus oder ohne Server über eine Test-Abstimmung im Spiel: Stimmen per Klick, sonst Zufall.

using System;
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
        /// <summary>Gewinner, der wartet, weil der Spiel-Erzähler gerade etwas ausgelöst hat.</summary>
        public static EventCandidate Pending { get; private set; }
        public static string LastResult { get; private set; }
        public static DateTime? LastExternalUtc { get; private set; }
        private static DateTime? _lastEndUtc;
        private static DateTime? _armedUtc;
        private static DateTime? _retryAtUtc;
        private static bool _firing;
        /// <summary>So lange auf die Antwort zum Start warten; danach gilt der Versuch als gescheitert.</summary>
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);
        /// <summary>So lange nach dem geplanten Ende auf <c>poll_result</c> warten (Server kontrolliert spätestens nach 5 min).</summary>
        private static readonly TimeSpan ResultTimeout = TimeSpan.FromMinutes(7);

        /// <summary>Echte Twitch-Abstimmung über den Server statt Test-Abstimmung im Spiel.</summary>
        public static bool UseTwitch => !Settings.testMode && Settings.eventTwitchPoll && Bridge.Link != null;

        private static CompanionSettings Settings => TwitchCompanionMod.Settings;

        public static VoteTiming Timing => new VoteTiming
        {
            Grace = TimeSpan.FromMinutes(Math.Max(0, Settings.eventGraceMinutes)),
            Interval = TimeSpan.FromMinutes(Math.Max(1, Settings.eventIntervalMinutes)),
            Quiet = TimeSpan.FromMinutes(Math.Max(0, Settings.eventQuietMinutes)),
        };

        /// <summary>Darf gerade abgestimmt werden? Wie Befehle: Brücke aktiv und Kategorie RimWorld, im Testmodus immer.</summary>
        private static bool Allowed =>
            Settings.eventVotes && Bridge.IsActive && Find.TickManager != null && (Settings.testMode || (Bridge.Link?.GateOpen ?? false));

        /// <summary>Gnadenfrist ab Streambeginn; ohne bekannten Beginn (Testmodus) ab dem Einschalten.</summary>
        private static DateTime? GraceFrom => !Settings.testMode && Settings.StreamStartedUtc.HasValue ? Settings.StreamStartedUtc : _armedUtc;

        /// <summary>P13: Ereignis, das nicht aus der Abstimmung kam (Spiel-Erzähler, RICS-Kauf, Quest).</summary>
        public static void NoteExternalEvent()
        {
            if (!_firing) LastExternalUtc = DateTime.UtcNow;
        }

        /// <summary>Neuer Stream: gewonnene Ereignisse dürfen wieder kommen, Gnadenfrist beginnt neu.</summary>
        public static void OnNewStream()
        {
            Settings.eventsUsedThisStream?.Clear();
            _lastEndUtc = null;
        }

        /// <summary>Im Hauptthread, aus <see cref="Bridge.MainThreadTick"/>.</summary>
        public static void Tick()
        {
            DateTime now = DateTime.UtcNow;
            VoteTiming timing = Timing;
            if (Pending != null)
            {
                if (VoteSchedule.MayFire(LastExternalUtc, now, timing)) Execute(Pending, now);
                return;
            }
            VoteRound round = Current;
            if (round != null)
            {
                if (round.AwaitingStart)
                {
                    if (now - round.StartedUtc > StartTimeout) Abort(round, "Keine Antwort vom Server auf den Start der Abstimmung.", now, TimeSpan.FromMinutes(2));
                }
                else if (round.ServerPollId != null)
                {
                    if (now > round.EndsUtc + ResultTimeout) Abort(round, "Kein Ergebnis vom Server erhalten (Verbindung oder Neustart).", now, null);
                }
                else if (round.IsDue(now)) Finish(round, round.Resolve(Rng), now);
                return;
            }
            if (!Allowed) { _armedUtc = null; return; }
            _armedUtc ??= now;
            if (_retryAtUtc.HasValue)
            {
                if (now < _retryAtUtc.Value || !VoteSchedule.MayFire(LastExternalUtc, now, timing)) return;
                _retryAtUtc = null;
                Start(now);
                return;
            }
            if (VoteSchedule.IsDue(GraceFrom, _lastEndUtc, LastExternalUtc, now, timing))
                Start(now);
        }

        /// <summary>Antwort des Servers auf den Start (Hauptthread).</summary>
        public static void OnPollStart(PollStartEvent ev)
        {
            VoteRound round = Current;
            if (round == null || !round.AwaitingStart || ev.RoundId != round.Id) return;
            DateTime now = DateTime.UtcNow;
            if (ev.Result.Ok)
            {
                round.AwaitingStart = false;
                round.ServerPollId = ev.Result.PollId;
                if (ev.Result.EndsAt.HasValue) round.EndsUtc = ev.Result.EndsAt.Value.ToUniversalTime();
                Messages.Message("Abstimmung läuft: " + string.Join(" · ", round.OptionTitles), MessageTypeDefOf.NeutralEvent, false);
                Log.Message(LogPrefix + "Twitch-Abstimmung gestartet (" + round.Options.Count + " Optionen"
                    + (round.ChannelPointsPerVote > 0 ? ", Zusatzstimmen " + round.ChannelPointsPerVote + " Kanalpunkte" : string.Empty) + ").");
                return;
            }
            Abort(round, ConsumerPolls.Describe(ev.Result), now, ConsumerPolls.RetryAfter(ev.Result, now));
        }

        /// <summary>Ergebnis der Twitch-Abstimmung (Hauptthread); nur für die eigene laufende Runde.</summary>
        public static void OnPollResult(PollResultEvent ev)
        {
            VoteRound round = Current;
            if (round?.ServerPollId == null || ev.Result?.PollId != round.ServerPollId) return;
            EventCandidate winner = round.ApplyServerResult(ev.Result, Rng);
            if (winner == null)
            {
                Abort(round, "Abstimmung ohne Ergebnis beendet (" + ev.Result.Status + ").", DateTime.UtcNow, null);
                return;
            }
            Finish(round, winner, DateTime.UtcNow);
        }

        /// <summary>Runde ohne Ereignis beenden; mit <paramref name="retry"/> neuer Versuch nach dieser Zeit, sonst in der nächsten Runde.</summary>
        private static void Abort(VoteRound round, string why, DateTime nowUtc, TimeSpan? retry)
        {
            if (round != null) round.State = VoteState.Cancelled;
            Current = null;
            _lastEndUtc = nowUtc;
            _retryAtUtc = retry.HasValue ? nowUtc + retry.Value : (DateTime?)null;
            LastResult = DateTime.Now.ToString("HH:mm") + ": " + why + (retry.HasValue ? " Neuer Versuch in " + Math.Max(1, (int)Math.Round(retry.Value.TotalMinutes)) + " min." : string.Empty);
            Messages.Message(why, MessageTypeDefOf.RejectInput, false);
            Log.Message(LogPrefix + "Ereignis-Abstimmung: " + LastResult);
        }

        /// <summary>Neue Runde mit Vorschlägen des Erzählers; falsch, wenn nicht genug Ereignisse möglich sind.</summary>
        public static bool Start(DateTime nowUtc)
        {
            if (Current != null || Pending != null || global::Verse.Current.Game == null) return false;
            Map map = HomeMap;
            List<EventCandidate> options = EventPlanner.Propose(Candidates(), Settings.eventOptions, Rng,
                Settings.eventsUsedThisStream, map?.weatherDecider != null);
            if (!EventPlanner.Enough(options))
            {
                _lastEndUtc = nowUtc; // nicht in jedem Takt neu versuchen
                LastResult = "Keine Abstimmung: zu wenige Ereignisse möglich (" + options.Count(o => !o.IsCalm) + ").";
                Log.Message(LogPrefix + LastResult);
                return false;
            }
            Current = new VoteRound(options, nowUtc, Settings.eventVoteSeconds) { ChannelPointsPerVote = VoteSchedule.ChannelPoints(Rng) };
            if (UseTwitch)
            {
                Current.AwaitingStart = true;
                Bridge.Link.StartPoll(Current.Id, ConsumerPolls.BuildStartBody(ServerLink.Client, Current));
            }
            else Messages.Message("Abstimmung (Test): " + string.Join(" · ", Current.OptionTitles), MessageTypeDefOf.NeutralEvent, false);
            if (Settings.eventOverlay) Find.WindowStack.Add(new EventVoteWindow());
            return true;
        }

        private static void Finish(VoteRound round, EventCandidate winner, DateTime nowUtc)
        {
            Current = null;
            _lastEndUtc = nowUtc;
            _retryAtUtc = null;
            if (winner == null) return;
            LastResult = DateTime.Now.ToString("HH:mm") + ": „" + winner.Label + "“ (" + round.Votes[round.WinnerIndex] + " Stimmen)";
            if (!winner.IsCalm) Settings.MarkEventUsed(winner.DefName);
            if (VoteSchedule.MayFire(LastExternalUtc, nowUtc, Timing)) { Execute(winner, nowUtc); return; }
            Pending = winner;
            Messages.Message("Der Chat hat gewählt: " + winner.Label + ". Der Erzähler wartet noch kurz.", MessageTypeDefOf.NeutralEvent, false);
        }

        private static void Execute(EventCandidate winner, DateTime nowUtc)
        {
            Pending = null;
            _lastEndUtc = nowUtc;
            bool fired = winner.Calm == CalmKind.Nothing || Fire(winner);
            if (!fired) LastResult += " – ließ sich nicht auslösen";
            Messages.Message("Der Chat hat gewählt: " + winner.Label + (fired ? "." : " – geht gerade nicht."),
                fired ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
            Log.Message(LogPrefix + "Ereignis-Abstimmung: " + LastResult);
        }

        /// <summary>Test-Abstimmung sofort auswerten (Knopf im Fenster).</summary>
        public static void FinishNow()
        {
            if (Current != null && Current.ServerPollId == null && !Current.AwaitingStart) Finish(Current, Current.Resolve(Rng), DateTime.UtcNow);
        }

        public static void Cancel()
        {
            if (Current == null) return;
            Current.State = VoteState.Cancelled;
            Current = null;
            _lastEndUtc = DateTime.UtcNow;
        }

        // ── Ereignisse ─────────────────────────────────────────────────

        private static Map HomeMap => Find.Maps?.Where(m => m.IsPlayerHome).InRandomOrder().FirstOrDefault();

        /// <summary>Ereignisse aus der eigenen Liste (<see cref="EventCatalog"/>), die es im Spiel gibt und die gerade auslösbar sind.</summary>
        public static List<EventCandidate> Candidates()
        {
            var result = new List<EventCandidate>();
            Map map = HomeMap;
            if (map == null) return result;
            foreach (CatalogEntry e in EventCatalog.Default)
            {
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(e.DefName);
                if (def?.Worker == null || !CanFire(def, map)) continue;
                result.Add(new EventCandidate { DefName = def.defName, Label = def.LabelCap.ToString(), Karma = e.Karma, Weight = def.baseChance });
            }
            return result;
        }

        /// <summary>Alle Einträge der Liste mit Spielnamen und ob es sie im Spiel gibt (für die Einstellungen).</summary>
        public static IEnumerable<string> CatalogStatus()
        {
            foreach (CatalogEntry e in EventCatalog.Default)
            {
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(e.DefName);
                yield return (def == null ? "– " : "• ") + (def?.LabelCap.ToString() ?? e.DefName) + " (" + VoteRound.KarmaWord(new EventCandidate { Karma = e.Karma })
                    + (def == null ? ", nicht im Spiel" : string.Empty) + ")";
            }
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

        /// <summary>Löst das Ereignis aus wie RICS (<c>DefaultParmsNow</c>, erzwungen); Wetterwechsel wie im Spiel vorgesehen.</summary>
        public static bool Fire(EventCandidate c)
        {
            _firing = true;
            try
            {
                if (c.Calm == CalmKind.Weather)
                {
                    Map home = HomeMap;
                    if (home?.weatherDecider == null) return false;
                    home.weatherDecider.StartNextWeather();
                    return true;
                }
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(c.DefName);
                if (def?.Worker == null) return false;
                foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome).InRandomOrder())
                {
                    try
                    {
                        IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                        parms.forced = true;
                        if (def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms)) return true;
                    }
                    catch (Exception ex) { Log.Warning(LogPrefix + "Ereignis " + c.DefName + " nicht ausgelöst: " + ex.Message); }
                }
                return false;
            }
            finally { _firing = false; }
        }
    }

    /// <summary>P13: merkt Ereignisse des Spiel-Erzählers (und alle anderen, die nicht aus der Abstimmung kommen).</summary>
    internal static class IncidentWatch
    {
        public static void Apply(Harmony harmony) =>
            harmony.Patch(AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute)),
                postfix: new HarmonyMethod(typeof(IncidentWatch), nameof(Postfix)));

        private static void Postfix(bool __result)
        {
            if (__result) EventVotes.NoteExternalEvent();
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
            bool twitch = round.ServerPollId != null || round.AwaitingStart;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), round.Title);
            Text.Font = GameFont.Small;
            string state = round.AwaitingStart ? "Abstimmung startet …"
                : "Noch " + (int)left.TotalMinutes + ":" + left.Seconds.ToString("00") + (twitch && left == TimeSpan.Zero ? " – Ergebnis kommt gleich" : string.Empty);
            Widgets.Label(new Rect(0f, 32f, inRect.width, 24f), state
                + (round.ChannelPointsPerVote > 0 ? " · Zusatzstimmen: " + round.ChannelPointsPerVote + " Kanalpunkte" : string.Empty)
                + (twitch ? " · Abstimmen über Twitch" : " · Test-Abstimmung"));
            float y = 60f;
            for (int i = 0; i < round.Options.Count; i++)
            {
                EventCandidate o = round.Options[i];
                Widgets.Label(new Rect(0f, y, inRect.width - 120f, 30f),
                    (i + 1) + ". " + o.Label + " (" + VoteRound.KarmaWord(o) + ")" + (twitch ? string.Empty : " – " + round.Votes[i]));
                int index = i;
                if (!twitch)
                {
                    if (Widgets.ButtonText(new Rect(inRect.width - 110f, y, 50f, 26f), "+1")) round.AddVote(index);
                    if (Widgets.ButtonText(new Rect(inRect.width - 55f, y, 55f, 26f), "+10")) round.AddVote(index, 10);
                }
                y += 34f;
            }
            if (twitch)
            {
                if (Widgets.ButtonText(new Rect(0f, inRect.height - 26f, 200f, 26f), "Ergebnis ignorieren")) EventVotes.Cancel();
                return;
            }
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 26f, 140f, 26f), "Jetzt auswerten")) EventVotes.FinishNow();
            if (Widgets.ButtonText(new Rect(150f, inRect.height - 26f, 120f, 26f), "Abbrechen")) EventVotes.Cancel();
        }
    }
}
