// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ereignis-Abstimmung (Stufe 3, Feature-Issue #4): Der Erzähler schlägt vor, der Chat wählt.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Einordnung eines Ereignisses laut RICS (<c>BuyableIncident.KarmaType</c>).</summary>
    /// <summary>Einordnung eines Ereignisses; <c>Doom</c> und <c>Blessing</c> nur über die ausgleichende Gerechtigkeit.</summary>
    public enum EventKarma { Good, Neutral, Bad, Doom, Blessing }

    /// <summary>Ruhige Optionen ohne Spielereignis (Nutzer, 20.11.2026).</summary>
    public enum CalmKind { None, Nothing, Weather }

    public sealed class EventCandidate
    {
        public const string NothingDefName = "tc_nothing", WeatherDefName = "tc_weather";
        public static EventCandidate Nothing => new EventCandidate { DefName = NothingDefName, Label = "Nichts passiert", Karma = EventKarma.Neutral, Calm = CalmKind.Nothing, Weight = 1f };
        public static EventCandidate Weather => new EventCandidate { DefName = WeatherDefName, Label = "Wetterwechsel", Karma = EventKarma.Neutral, Calm = CalmKind.Weather, Weight = 1f };

        public CalmKind Calm;
        public bool IsCalm => Calm != CalmKind.None;
        public string DefName;
        public string Label;
        public EventKarma Karma;
        /// <summary>Grundhäufigkeit im Spiel (<c>IncidentDef.baseChance</c>); 0 zählt wie ein kleiner Wert.</summary>
        public float Weight;

        public static EventKarma ParseKarma(string karma)
        {
            switch ((karma ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "good": return EventKarma.Good;
                case "bad": return EventKarma.Bad;
                case "doom": return EventKarma.Doom;
                default: return EventKarma.Neutral;
            }
        }
    }

    /// <summary>
    /// Der „Erzähler“ (Vorschlag von Claude, Nutzer 20.11.2026: Erzähler schlägt vor, Chat wählt): je Runde möglichst ein
    /// gutes und ein schlechtes Ereignis und eine ruhige Option („Nichts passiert“ oder „Wetterwechsel“), dann neutrale;
    /// gewichtet nach der Häufigkeit im Spiel. Verheerendes kommt nicht vor (später Gesamt-Karma). Was in diesem Stream schon
    /// gewonnen hat, kommt nur, wenn sonst nichts dieser Sorte übrig ist.
    /// </summary>
    public static class EventPlanner
    {
        public const int MinOptions = 2;
        public const int MaxOptions = 5;

        public static List<EventCandidate> Propose(IEnumerable<EventCandidate> candidates, int count, Random rng,
            ICollection<string> usedThisStream = null, bool weatherPossible = true)
        {
            count = Math.Max(MinOptions, Math.Min(MaxOptions, count));
            var pool = (candidates ?? Enumerable.Empty<EventCandidate>())
                .Where(c => c != null && !string.IsNullOrEmpty(c.DefName) && !c.IsCalm && c.Karma != EventKarma.Doom && c.Karma != EventKarma.Blessing)
                .GroupBy(c => c.DefName).Select(g => g.First())
                .ToList();
            bool Used(EventCandidate c) => usedThisStream != null && usedThisStream.Contains(c.DefName);
            EventCandidate Take(Func<EventCandidate, bool> filter)
            {
                var sort = pool.Where(filter).ToList();
                EventCandidate pick = PickWeighted(sort.Where(c => !Used(c)).ToList(), rng) ?? PickWeighted(sort, rng);
                if (pick != null) pool.Remove(pick);
                return pick;
            }

            var chosen = new List<EventCandidate>();
            void Add(EventCandidate c) { if (c != null && chosen.Count < count) chosen.Add(c); }
            Add(Take(c => c.Karma == EventKarma.Good));
            Add(Take(c => c.Karma == EventKarma.Bad));
            Add(weatherPossible && rng.Next(2) == 0 ? EventCandidate.Weather : EventCandidate.Nothing);
            while (chosen.Count < count)
            {
                EventCandidate next = Take(c => c.Karma == EventKarma.Neutral) ?? Take(_ => true);
                if (next == null) break;
                Add(next);
            }
            // Reihenfolge mischen, damit „gut“ nicht immer oben steht.
            return chosen.OrderBy(_ => rng.Next()).ToList();
        }

        /// <summary>
        /// Ausgleichende Gerechtigkeit: ersetzt die ruhige oder eine neutrale Option durch ein besonderes Ereignis
        /// (verheerend bzw. Segen). Es bleibt eine Wahl; der Chat kann es abwenden.
        /// </summary>
        public static List<EventCandidate> AddJustice(List<EventCandidate> options, EventCandidate special)
        {
            if (options == null || special == null || options.Any(o => o.DefName == special.DefName)) return options;
            int i = options.FindIndex(o => o.IsCalm);
            if (i < 0) i = options.FindIndex(o => o.Karma == EventKarma.Neutral);
            if (i < 0) options.Add(special); else options[i] = special;
            return options;
        }

        /// <summary>Mindestens zwei echte Ereignisse, sonst lohnt keine Abstimmung.</summary>
        public static bool Enough(IList<EventCandidate> options) => options != null && options.Count(o => !o.IsCalm) >= 2;

        private static EventCandidate PickWeighted(IList<EventCandidate> list, Random rng)
        {
            if (list == null || list.Count == 0) return null;
            double total = list.Sum(Weight);
            double r = rng.NextDouble() * total;
            foreach (EventCandidate c in list)
            {
                r -= Weight(c);
                if (r < 0) return c;
            }
            return list[list.Count - 1];
        }

        private static double Weight(EventCandidate c) => Math.Max(0.1, c.Weight);
    }

    public enum VoteState { Running, Resolved, Cancelled }

    /// <summary>
    /// Eine Abstimmungsrunde. Wer abstimmen lässt (Test-Abstimmung im Spiel, später der Server über Issue #81), trägt
    /// Stimmen ein oder meldet das Ergebnis; die Runde selbst kennt keinen Kanal.
    /// </summary>
    public sealed class VoteRound
    {
        /// <summary>Twitch-Grenzen: Titel bis 60, Optionen bis 25 Zeichen, 15 bis 1800 s.</summary>
        public const int TitleMax = 60, OptionMax = 25, MinSeconds = 15, MaxSeconds = 1800;
        public const string DefaultTitle = "Was schickt der Erzähler als Nächstes?";

        public string Id = Guid.NewGuid().ToString("N");
        public string Title = DefaultTitle;
        public List<EventCandidate> Options;
        public int[] Votes;
        public DateTime StartedUtc;
        public DateTime EndsUtc;
        /// <summary>Kanalpunkte je Zusatzstimme, 0 = keine (bei etwa jeder dritten Abstimmung 3000).</summary>
        public int ChannelPointsPerVote;
        public VoteState State = VoteState.Running;
        public int WinnerIndex = -1;
        /// <summary>Twitch-Abstimmung über den Server: ID aus <c>POST /api/twitch/poll/start</c>, sonst <c>null</c> (Test-Abstimmung).</summary>
        public string ServerPollId;
        /// <summary>Auf die Antwort des Servers warten (Start läuft im Hintergrund).</summary>
        public bool AwaitingStart;

        public VoteRound(IList<EventCandidate> options, DateTime nowUtc, int seconds)
        {
            Options = options.ToList();
            Votes = new int[Options.Count];
            StartedUtc = nowUtc;
            EndsUtc = nowUtc.AddSeconds(Math.Max(MinSeconds, Math.Min(MaxSeconds, seconds)));
        }

        public EventCandidate Winner => WinnerIndex >= 0 && WinnerIndex < Options.Count ? Options[WinnerIndex] : null;
        public TimeSpan Remaining(DateTime nowUtc) => State == VoteState.Running && EndsUtc > nowUtc ? EndsUtc - nowUtc : TimeSpan.Zero;
        public bool IsDue(DateTime nowUtc) => State == VoteState.Running && nowUtc >= EndsUtc;

        public void AddVote(int index, int count = 1)
        {
            if (State == VoteState.Running && index >= 0 && index < Votes.Length && count > 0) Votes[index] += count;
        }

        /// <summary>Wertet aus: meiste Stimmen gewinnt; bei Gleichstand (auch ohne Stimmen) lost die Runde unter den Besten.</summary>
        public EventCandidate Resolve(Random rng)
        {
            if (State != VoteState.Running) return Winner;
            int best = Votes.Length == 0 ? 0 : Votes.Max();
            var top = Enumerable.Range(0, Votes.Length).Where(i => Votes[i] == best).ToList();
            WinnerIndex = top.Count == 0 ? -1 : top[rng.Next(top.Count)];
            State = VoteState.Resolved;
            return Winner;
        }

        public static string Shorten(string text, int max)
        {
            text = (text ?? string.Empty).Trim();
            return text.Length <= max ? text : text.Substring(0, max - 1).TrimEnd() + "…";
        }

        /// <summary>Gekürzte, eindeutige Titel der Optionen (Twitch: bis 25 Zeichen); gleich lautende bekommen eine Nummer.</summary>
        public IList<string> OptionTitles
        {
            get
            {
                var titles = new List<string>();
                foreach (EventCandidate o in Options)
                {
                    string t = Shorten(o.Label, OptionMax);
                    for (int n = 2; titles.Contains(t, StringComparer.OrdinalIgnoreCase); n++)
                        t = Shorten(o.Label, OptionMax - 2 - n.ToString().Length) + " " + n;
                    titles.Add(t);
                }
                return titles;
            }
        }

        /// <summary>
        /// Übernimmt das Ergebnis der Twitch-Abstimmung (<c>poll_result</c>): Stimmen je Option nach Titel, Gewinner aus
        /// <c>winners</c> (bei Gleichstand Los). Abgebrochene oder entfernte Abstimmungen geben keinen Gewinner.
        /// </summary>
        public EventCandidate ApplyServerResult(PollResult result, Random rng)
        {
            if (State != VoteState.Running || result == null) return null;
            IList<string> titles = OptionTitles;
            int IndexOf(string title) => titles.Select((t, i) => new { t, i })
                .FirstOrDefault(x => string.Equals(x.t.Trim(), (title ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))?.i ?? -1;
            foreach (PollChoiceResult c in result.Choices)
            {
                int i = IndexOf(c.Title);
                if (i >= 0) Votes[i] = c.Votes;
            }
            if (!result.IsCompleted) { State = VoteState.Cancelled; return null; }
            var winners = result.WinnerTitles.Select(IndexOf).Where(i => i >= 0).Distinct().ToList();
            if (winners.Count == 0) return Resolve(rng); // Titel unbekannt: nach den Stimmen
            WinnerIndex = winners[rng.Next(winners.Count)];
            State = VoteState.Resolved;
            return Winner;
        }

        public static string KarmaWord(EventCandidate c) =>
            c.IsCalm ? "ruhig" : c.Karma == EventKarma.Good ? "gut" : c.Karma == EventKarma.Bad ? "schlecht" : c.Karma == EventKarma.Doom ? "verheerend" : c.Karma == EventKarma.Blessing ? "Segen" : "neutral";
    }

    /// <summary>Zeitregeln (Nutzer, 20.11.2026): Gnadenfrist zu Streambeginn, Abstand, Ruhe nach Ereignissen des Spiel-Erzählers.</summary>
    public sealed class VoteTiming
    {
        public TimeSpan Grace = TimeSpan.FromMinutes(60);
        public TimeSpan Interval = TimeSpan.FromMinutes(30);
        public TimeSpan Quiet = TimeSpan.FromMinutes(10);
    }

    /// <summary>Wann die nächste Runde fällig ist und ob Zusatzstimmen gelten.</summary>
    public static class VoteSchedule
    {
        public const int ExtraVoteChannelPoints = 3000;

        /// <param name="graceFromUtc">Streambeginn (oder Einschalten, wenn er unbekannt ist).</param>
        /// <param name="lastEndUtc">Ende der letzten eigenen Runde (Auslösen des Gewinners).</param>
        /// <param name="lastExternalUtc">Letztes Ereignis, das nicht aus der Abstimmung kam (Spiel-Erzähler, RICS).</param>
        public static bool IsDue(DateTime? graceFromUtc, DateTime? lastEndUtc, DateTime? lastExternalUtc, DateTime nowUtc, VoteTiming t)
        {
            if (graceFromUtc == null || nowUtc - graceFromUtc.Value < t.Grace) return false;
            if (lastEndUtc.HasValue && nowUtc - lastEndUtc.Value < t.Interval) return false;
            return MayFire(lastExternalUtc, nowUtc, t);
        }

        /// <summary>Gewinner erst auslösen, wenn seit dem letzten fremden Ereignis genug Ruhe war; sonst warten.</summary>
        public static bool MayFire(DateTime? lastExternalUtc, DateTime nowUtc, VoteTiming t) =>
            !lastExternalUtc.HasValue || nowUtc - lastExternalUtc.Value >= t.Quiet;

        /// <summary>Zusatzstimmen mit Kanalpunkten bei etwa jeder dritten Abstimmung (Nutzer, 19.11.2026).</summary>
        public static int ChannelPoints(Random rng) => rng.Next(3) == 0 ? ExtraVoteChannelPoints : 0;
    }
}
