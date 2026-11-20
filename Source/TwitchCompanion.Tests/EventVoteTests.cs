// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    public class EventVoteTests
    {
        private static EventCandidate C(string name, EventKarma k, float w = 1f) => new EventCandidate { DefName = name, Label = name, Karma = k, Weight = w };

        private static readonly List<EventCandidate> Pool = new List<EventCandidate>
        {
            C("Händler", EventKarma.Good), C("Wanderer", EventKarma.Good),
            C("Raid", EventKarma.Bad), C("Seuche", EventKarma.Bad),
            C("Sonnenfinsternis", EventKarma.Neutral),
            C("GiftigerFallout", EventKarma.Doom),
        };

        private static readonly DateTime T0 = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void ErzaehlerMischtGutNeutralSchlecht()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var o = EventPlanner.Propose(Pool, 3, new Random(seed), includeDoom: false);
                Assert.Equal(3, o.Count);
                Assert.Equal(3, o.Select(x => x.DefName).Distinct().Count());
                Assert.Contains(o, x => x.Karma == EventKarma.Good);
                Assert.Contains(o, x => x.Karma == EventKarma.Bad);
                Assert.Contains(o, x => x.Karma == EventKarma.Neutral);
                Assert.DoesNotContain(o, x => x.Karma == EventKarma.Doom);
            }
        }

        [Fact]
        public void VerheerendNurAufWunsch()
        {
            var o = EventPlanner.Propose(Pool, 5, new Random(1), includeDoom: true);
            Assert.Contains(o, x => x.Karma == EventKarma.Doom);
        }

        [Fact]
        public void FehlendeSorteWirdAufgefuellt()
        {
            var onlyBad = new[] { C("Raid", EventKarma.Bad), C("Seuche", EventKarma.Bad), C("Insekten", EventKarma.Bad) };
            Assert.Equal(3, EventPlanner.Propose(onlyBad, 3, new Random(2), false).Count);
            Assert.Single(EventPlanner.Propose(new[] { C("Raid", EventKarma.Bad) }, 3, new Random(2), false));
        }

        [Fact]
        public void LetzterGewinnerKommtNichtGleichWieder()
        {
            for (int seed = 0; seed < 30; seed++)
                Assert.DoesNotContain(EventPlanner.Propose(Pool, 3, new Random(seed), false, "Sonnenfinsternis"), x => x.DefName == "Sonnenfinsternis");
        }

        [Fact]
        public void GewichtZaehlt()
        {
            var pool = new[] { C("Häufig", EventKarma.Good, 10f), C("Selten", EventKarma.Good, 0.1f), C("Raid", EventKarma.Bad) };
            int haeufig = Enumerable.Range(0, 500).Count(s => EventPlanner.Propose(pool, 2, new Random(s), false).Any(x => x.DefName == "Häufig"));
            Assert.True(haeufig > 450, haeufig.ToString());
        }

        [Fact]
        public void MeisteStimmenGewinnen()
        {
            var r = new VoteRound(Pool.Take(3).ToList(), T0, 120);
            r.AddVote(1, 5);
            r.AddVote(2, 3);
            Assert.Equal("Wanderer", r.Resolve(new Random(0)).DefName);
            Assert.Equal(VoteState.Resolved, r.State);
            r.AddVote(0, 99);                                   // nach dem Ende zählt nichts mehr
            Assert.Equal(0, r.Votes[0]);
        }

        [Fact]
        public void GleichstandUndKeineStimmenLosen()
        {
            var winners = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++)
                winners.Add(new VoteRound(Pool.Take(3).ToList(), T0, 120).Resolve(new Random(seed)).DefName);
            Assert.Equal(3, winners.Count);
        }

        [Fact]
        public void DauerUndRestzeit()
        {
            var r = new VoteRound(Pool.Take(3).ToList(), T0, 5);   // Twitch: mindestens 15 s
            Assert.Equal(T0.AddSeconds(15), r.EndsUtc);
            Assert.False(r.IsDue(T0.AddSeconds(14)));
            Assert.True(r.IsDue(T0.AddSeconds(15)));
            Assert.Equal(T0.AddSeconds(1800), new VoteRound(Pool, T0, 99999).EndsUtc);
        }

        [Fact]
        public void TitelUndOptionenPassenZuTwitch()
        {
            Assert.True(VoteRound.DefaultTitle.Length <= VoteRound.TitleMax);
            Assert.Equal(25, VoteRound.Shorten("Psychische Drohne in der ganzen Gegend", VoteRound.OptionMax).Length);
            Assert.Equal("Raid", VoteRound.Shorten(" Raid ", 25));
        }

        [Fact]
        public void Zeitplan()
        {
            var every = TimeSpan.FromMinutes(20);
            Assert.False(VoteSchedule.IsDue(null, null, T0, every));
            Assert.False(VoteSchedule.IsDue(null, T0, T0.AddMinutes(4), every));
            Assert.True(VoteSchedule.IsDue(null, T0, T0.AddMinutes(5), every));
            Assert.False(VoteSchedule.IsDue(T0, T0, T0.AddMinutes(19), every));
            Assert.True(VoteSchedule.IsDue(T0, T0, T0.AddMinutes(20), every));
        }

        [Fact]
        public void ZusatzstimmenEtwaJedeDritte()
        {
            var rng = new Random(5);
            int with = Enumerable.Range(0, 3000).Count(_ => VoteSchedule.ChannelPoints(rng) == 3000);
            Assert.InRange(with, 850, 1150);
        }

        [Theory]
        [InlineData("Good", EventKarma.Good)]
        [InlineData("doom", EventKarma.Doom)]
        [InlineData("Bad", EventKarma.Bad)]
        [InlineData(null, EventKarma.Neutral)]
        [InlineData("Unbekannt", EventKarma.Neutral)]
        public void KarmaAusRics(string s, EventKarma k) => Assert.Equal(k, EventCandidate.ParseKarma(s));
    }
}
