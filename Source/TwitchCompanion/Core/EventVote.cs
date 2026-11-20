// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ereignis-Abstimmung (Stufe 3, Feature-Issue #4): Der Erzähler schlägt vor, der Chat wählt.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Einordnung eines Ereignisses laut RICS (<c>BuyableIncident.KarmaType</c>).</summary>
    public enum EventKarma { Good, Neutral, Bad, Doom }

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
                .Where(c => c != null && !string.IsNullOrEmpty(c.DefName) && !c.IsCalm && c.Karma != EventKarma.Doom)
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

        public IEnumerable<string> OptionTitles => Options.Select(o => Shorten(o.Label, OptionMax));

        public static string KarmaWord(EventCandidate c) =>
            c.IsCalm ? "ruhig" : c.Karma == EventKarma.Good ? "gut" : c.Karma == EventKarma.Bad ? "schlecht" : c.Karma == EventKarma.Doom ? "verheerend" : "neutral";
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
