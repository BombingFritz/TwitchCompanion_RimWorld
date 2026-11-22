// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    public class KarmaTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 22, 20, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan HalfLife = TimeSpan.FromHours(3);

        [Fact]
        public void GuteEinloesungenHebenSchlechteSenken()
        {
            var c = new ColonyKarma();
            for (int i = 0; i < 10; i++) c.Add(KarmaRules.ColonyDelta(KarmaAction.ItemPurchase), T0, HalfLife);
            Assert.Equal(10f, c.Value, 3);
            c.Add(KarmaRules.ColonyDelta(KarmaAction.RabbitPurchase, 4), T0, HalfLife);
            Assert.Equal(2f, c.Value, 3);
            Assert.Equal(0f, KarmaRules.ColonyDelta(KarmaAction.RabbitPurchase, 0));   // beim Kauf zählt erst der Angriff
        }

        [Fact]
        public void KlingtMitHalbwertszeitAbUndBleibtImRahmen()
        {
            var c = new ColonyKarma();
            c.Add(80f, T0, HalfLife);
            c.Decay(T0.AddHours(3), HalfLife);
            Assert.Equal(40f, c.Value, 2);
            c.Add(500f, T0.AddHours(3), HalfLife);
            Assert.Equal(ColonyKarma.Max, c.Value);
            c.Add(-1000f, T0.AddHours(3), HalfLife);
            Assert.Equal(ColonyKarma.Min, c.Value);
        }

        [Fact]
        public void ZuschauerDeltas()
        {
            Assert.Equal(2f, KarmaRules.ViewerDelta(KarmaAction.ItemPurchase));
            Assert.Equal(-20f, KarmaRules.ViewerDelta(KarmaAction.RabbitPurchase));
            Assert.Equal(-10f, KarmaRules.ViewerDelta(KarmaAction.RabbitJoin));
            Assert.Equal(-10f, KarmaRules.ViewerDelta(KarmaAction.RaidJoin));
            Assert.Equal(0f, KarmaRules.ViewerDelta(KarmaAction.VoteGood));
        }

        [Fact]
        public void AbstimmungZaehltNachEinordnung()
        {
            Assert.Equal(KarmaAction.VoteGood, KarmaRules.ForVote(EventKarma.Good, false));
            Assert.Equal(KarmaAction.VoteNeutral, KarmaRules.ForVote(EventKarma.Neutral, true));
            Assert.Equal(-15f, KarmaRules.ColonyDelta(KarmaRules.ForVote(EventKarma.Doom, false)));
            Assert.Equal(15f, KarmaRules.ColonyDelta(KarmaRules.ForVote(EventKarma.Blessing, false)));
        }

        [Theory]
        [InlineData(39f, 40f, 0.6, 0.0)]
        [InlineData(40f, 40f, 0.6, 0.0)]
        [InlineData(70f, 40f, 0.6, 0.3)]
        [InlineData(-100f, 40f, 0.6, 0.6)]
        public void ChanceWaechstAbSchwelle(float colony, float threshold, double max, double expected) =>
            Assert.Equal(expected, Justice.Chance(colony, threshold, max), 3);

        [Fact]
        public void GutesKarmaBringtVerheerendesSchlechtesSegen()
        {
            var rng = new Random(3);
            var high = Enumerable.Range(0, 400).Select(_ => Justice.Roll(100f, 40f, 0.6, rng)).ToList();
            Assert.DoesNotContain(JusticeKind.Blessing, high);
            Assert.InRange(high.Count(k => k == JusticeKind.Doom), 190, 290);
            Assert.All(Enumerable.Range(0, 50).Select(_ => Justice.Roll(-100f, 40f, 1.0, rng)), k => Assert.Equal(JusticeKind.Blessing, k));
            Assert.All(Enumerable.Range(0, 50).Select(_ => Justice.Roll(10f, 40f, 1.0, rng)), k => Assert.Equal(JusticeKind.None, k));
        }

        [Fact]
        public void BesondereOptionErsetztDieRuhige()
        {
            var options = new List<EventCandidate>
            {
                new EventCandidate { DefName = "a", Label = "a", Karma = EventKarma.Good },
                EventCandidate.Nothing,
                new EventCandidate { DefName = "b", Label = "b", Karma = EventKarma.Bad },
            };
            var doom = new EventCandidate { DefName = "Infestation", Label = "Insekten", Karma = EventKarma.Doom };
            EventPlanner.AddJustice(options, doom);
            Assert.Equal(3, options.Count);
            Assert.DoesNotContain(options, o => o.IsCalm);
            Assert.Contains(doom, options);
            Assert.Same(options, EventPlanner.AddJustice(options, null));
        }

        [Fact]
        public void PlanerNimmtVerheerendUndSegenNieSelbst()
        {
            var pool = new[]
            {
                new EventCandidate { DefName = "g", Label = "g", Karma = EventKarma.Good },
                new EventCandidate { DefName = "b", Label = "b", Karma = EventKarma.Bad },
                new EventCandidate { DefName = "d", Label = "d", Karma = EventKarma.Doom },
                new EventCandidate { DefName = "s", Label = "s", Karma = EventKarma.Blessing },
            };
            for (int seed = 0; seed < 20; seed++)
                Assert.DoesNotContain(EventPlanner.Propose(pool, 5, new Random(seed)), o => o.Karma == EventKarma.Doom || o.Karma == EventKarma.Blessing);
        }

        [Fact]
        public void NeuerKolonistUndRaiderNachVorgabe()
        {
            Assert.Equal(5f, KarmaRules.ColonyDelta(KarmaAction.PawnPurchase));
            Assert.Equal(-4f, KarmaRules.ColonyDelta(KarmaAction.RaidJoin, 1));
            Assert.True(KarmaRules.ColonyDelta(KarmaAction.RaidJoin, 1) <= 2 * KarmaRules.ColonyDelta(KarmaAction.RabbitPurchase, 1));
        }

        [Fact]
        public void NiePyromaneNieFaul()
        {
            Assert.DoesNotContain(PawnFates.BadTraits, t => PawnFates.IsForbidden(t.Key, t.Value));
            Assert.True(PawnFates.IsForbidden("Pyromaniac", 0));
            Assert.True(PawnFates.IsForbidden("Industriousness", -2));
            Assert.True(PawnFates.IsForbidden("Industriousness", -1));
            Assert.False(PawnFates.IsForbidden("Industriousness", 2));
        }

        [Theory]
        [InlineData(20f, PawnFate.Cursed)]
        [InlineData(55f, PawnFate.Unlucky)]
        [InlineData(100f, PawnFate.Normal)]
        [InlineData(170f, PawnFate.Blessed)]
        public void KolonistNachKarma(float karma, PawnFate expected) => Assert.Equal(expected, PawnFates.For(karma));
    }
}
