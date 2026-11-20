// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ereignis-Abstimmung (Stufe 3, Feature-Issue #4): Der Erzähler schlägt vor, der Chat wählt.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Einordnung eines Ereignisses laut RICS (<c>BuyableIncident.KarmaType</c>).</summary>
    public enum EventKarma { Good, Neutral, Bad, Doom }

    public sealed class EventCandidate
    {
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
    /// gutes, ein neutrales und ein schlechtes Ereignis, gewichtet nach der Häufigkeit im Spiel; verheerende nur auf Wunsch.
    /// Fehlt eine Sorte, füllt er mit den übrigen auf. Das zuletzt gewählte Ereignis kommt nicht gleich wieder.
    /// </summary>
    public static class EventPlanner
    {
        public const int MinOptions = 2;
        public const int MaxOptions = 5;
        private static readonly EventKarma[] Order = { EventKarma.Good, EventKarma.Bad, EventKarma.Neutral, EventKarma.Doom };

        public static List<EventCandidate> Propose(IEnumerable<EventCandidate> candidates, int count, Random rng,
            bool includeDoom, string lastWinnerDefName = null)
        {
            count = Math.Max(MinOptions, Math.Min(MaxOptions, count));
            var pool = (candidates ?? Enumerable.Empty<EventCandidate>())
                .Where(c => c != null && !string.IsNullOrEmpty(c.DefName) && (includeDoom || c.Karma != EventKarma.Doom))
                .GroupBy(c => c.DefName).Select(g => g.First())
                .Where(c => c.DefName != lastWinnerDefName)
                .ToList();
            var chosen = new List<EventCandidate>();
            foreach (EventKarma karma in Order)
            {
                if (chosen.Count >= count) break;
                EventCandidate pick = PickWeighted(pool.Where(c => c.Karma == karma).ToList(), rng);
                if (pick == null) continue;
                chosen.Add(pick);
                pool.Remove(pick);
            }
            while (chosen.Count < count)
            {
                EventCandidate pick = PickWeighted(pool, rng);
                if (pick == null) break;
                chosen.Add(pick);
                pool.Remove(pick);
            }
            // Reihenfolge mischen, damit „gut“ nicht immer oben steht.
            return chosen.OrderBy(_ => rng.Next()).ToList();
        }

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

        public static string KarmaWord(EventKarma k) =>
            k == EventKarma.Good ? "gut" : k == EventKarma.Bad ? "schlecht" : k == EventKarma.Doom ? "verheerend" : "neutral";
    }

    /// <summary>Wann die nächste Runde fällig ist und ob Zusatzstimmen gelten.</summary>
    public static class VoteSchedule
    {
        /// <summary>Erste Runde nach dem Start erst nach dieser Zeit, damit nicht sofort abgestimmt wird.</summary>
        public static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(5);
        public const int ExtraVoteChannelPoints = 3000;

        public static bool IsDue(DateTime? lastEndUtc, DateTime? armedUtc, DateTime nowUtc, TimeSpan interval)
        {
            if (lastEndUtc.HasValue) return nowUtc - lastEndUtc.Value >= interval;
            return armedUtc.HasValue && nowUtc - armedUtc.Value >= FirstDelay;
        }

        /// <summary>Zusatzstimmen mit Kanalpunkten bei etwa jeder dritten Abstimmung (Nutzer, 19.11.2026).</summary>
        public static int ChannelPoints(Random rng) => rng.Next(3) == 0 ? ExtraVoteChannelPoints : 0;
    }
}
