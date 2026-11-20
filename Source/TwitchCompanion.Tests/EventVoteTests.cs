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
        public void ErzaehlerMischtGutSchlechtRuhigNeutral()
        {
            var calm = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++)
            {
                var o = EventPlanner.Propose(Pool, 4, new Random(seed));
                Assert.Equal(4, o.Count);
                Assert.Equal(4, o.Select(x => x.DefName).Distinct().Count());
                Assert.Contains(o, x => x.Karma == EventKarma.Good);
                Assert.Contains(o, x => x.Karma == EventKarma.Bad);
                Assert.Contains(o, x => x.Karma == EventKarma.Neutral && !x.IsCalm);
                Assert.Single(o, x => x.IsCalm);
                Assert.DoesNotContain(o, x => x.Karma == EventKarma.Doom);
                calm.Add(o.Single(x => x.IsCalm).DefName);
            }
            Assert.Equal(new[] { EventCandidate.NothingDefName, EventCandidate.WeatherDefName }.OrderBy(x => x), calm.OrderBy(x => x));
        }

        [Fact]
        public void OhneWetterNurNichts()
        {
            for (int seed = 0; seed < 20; seed++)
                Assert.Equal(CalmKind.Nothing, EventPlanner.Propose(Pool, 3, new Random(seed), null, weatherPossible: false).Single(x => x.IsCalm).Calm);
        }

        [Fact]
        public void VerheerendNie()
        {
            for (int seed = 0; seed < 20; seed++)
                Assert.DoesNotContain(EventPlanner.Propose(Pool, 5, new Random(seed)), x => x.Karma == EventKarma.Doom);
        }

        [Fact]
        public void FehlendeSorteWirdAufgefuellt()
        {
            var onlyBad = new[] { C("Raid", EventKarma.Bad), C("Seuche", EventKarma.Bad), C("Insekten", EventKarma.Bad) };
            var o = EventPlanner.Propose(onlyBad, 4, new Random(2));
            Assert.Equal(4, o.Count);
            Assert.True(EventPlanner.Enough(o));
            Assert.False(EventPlanner.Enough(EventPlanner.Propose(new[] { C("Raid", EventKarma.Bad) }, 3, new Random(2))));
        }

        [Fact]
        public void GewinnerDesStreamsKommenNichtWiederSolangeAuswahlBleibt()
        {
            var used = new HashSet<string> { "Händler", "Raid" };
            for (int seed = 0; seed < 30; seed++)
            {
                var o = EventPlanner.Propose(Pool, 3, new Random(seed), used);
                Assert.DoesNotContain(o, x => used.Contains(x.DefName));
            }
            var allGood = new HashSet<string> { "Händler", "Wanderer" };   // keine ungenutzten guten mehr: dann doch wieder
            Assert.Contains(EventPlanner.Propose(Pool, 3, new Random(1), allGood), x => x.Karma == EventKarma.Good);
        }

        [Fact]
        public void GewichtZaehlt()
        {
            var pool = new[] { C("Häufig", EventKarma.Good, 10f), C("Selten", EventKarma.Good, 0.1f), C("Raid", EventKarma.Bad) };
            int haeufig = Enumerable.Range(0, 500).Count(s => EventPlanner.Propose(pool, 3, new Random(s)).Any(x => x.DefName == "Häufig"));
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

        private static readonly VoteTiming Timing = new VoteTiming();

        [Fact]
        public void GnadenfristEineStundeAbStreambeginn()
        {
            Assert.False(VoteSchedule.IsDue(null, null, null, T0, Timing));
            Assert.False(VoteSchedule.IsDue(T0, null, null, T0.AddMinutes(59), Timing));
            Assert.True(VoteSchedule.IsDue(T0, null, null, T0.AddMinutes(60), Timing));
        }

        [Fact]
        public void DreissigMinutenAbstand()
        {
            DateTime end = T0.AddMinutes(70);
            Assert.False(VoteSchedule.IsDue(T0, end, null, end.AddMinutes(29), Timing));
            Assert.True(VoteSchedule.IsDue(T0, end, null, end.AddMinutes(30), Timing));
        }

        [Fact]
        public void ZehnMinutenRuheNachEreignisDesSpielErzaehlers()
        {
            DateTime ext = T0.AddMinutes(100);
            Assert.False(VoteSchedule.IsDue(T0, T0.AddMinutes(60), ext, ext.AddMinutes(9), Timing));
            Assert.True(VoteSchedule.IsDue(T0, T0.AddMinutes(60), ext, ext.AddMinutes(10), Timing));
            Assert.False(VoteSchedule.MayFire(ext, ext.AddMinutes(3), Timing));   // Gewinner wartet
            Assert.True(VoteSchedule.MayFire(ext, ext.AddMinutes(10), Timing));
            Assert.True(VoteSchedule.MayFire(null, ext, Timing));
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
