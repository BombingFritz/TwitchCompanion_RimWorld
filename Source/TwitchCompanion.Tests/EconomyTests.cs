// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    public class EconomyTests
    {
        [Fact]
        public void ZuschaustundeAusRics()
        {
            Assert.Equal(300, Economy.CoinsPerHour(10));     // RICS-Vorgabe: 10 alle 2 min
            Assert.Equal(30, Economy.CoinsPerHour(0));      // nie 0
        }

        [Theory]
        [InlineData(12, 10f, 120)]   // Bier ≈ 24 min bei 300/h
        [InlineData(1, 10f, 10)]     // Kartoffel je Stück
        [InlineData(0, 10f, 10)]     // nie 0
        [InlineData(480, 10f, 4800)] // Sturmgewehr
        public void ItemPreise(int standard, float factor, int expected) => Assert.Equal(expected, Economy.ScaledItemPrice(standard, factor));

        [Fact]
        public void KolonistUndXenotypen()
        {
            Assert.Equal(600, Economy.HoursToCoins(2f, 300));
            Assert.Equal(300, Economy.HoursToCoins(1f, 300));
            Assert.Equal(50, Economy.HoursToCoins(10f / 60f, 300));      // Spargel
            Assert.Equal(900, Economy.ScaleRelative(1500f, 1000f, 600)); // Xenotyp 1,5× Rasse bleibt 1,5×
            Assert.Equal(600, Economy.ScaleRelative(0f, 1000f, 600));
        }

        [Theory]
        [InlineData("Asparagus", null, true)]
        [InlineData("VCE_RawAsparagus", "raw asparagus", true)]
        [InlineData("Plant_X", "Spargel", true)]
        [InlineData("Beer", "beer", false)]
        public void Spargel(string defName, string label, bool expected) => Assert.Equal(expected, Economy.IsAsparagus(defName, label));

        [Fact]
        public void LootboxMeistBis150UndSelten()
        {
            var rng = new Random(1234);
            int n = 200000, over = 0, min = int.MaxValue, max = 0;
            for (int i = 0; i < n; i++)
            {
                int c = LootboxOdds.Roll(750, rng.NextDouble);
                if (c > LootboxOdds.Threshold) over++;
                min = Math.Min(min, c); max = Math.Max(max, c);
            }
            double share = (double)over / n;
            Assert.InRange(share, 0.045, 0.055);      // 1 zu 20
            Assert.Equal(LootboxOdds.LowMin, min);
            Assert.InRange(max, 700, 750);
        }

        [Fact]
        public void LootboxGrenzenAuchBeiZufall1()
        {
            Assert.Equal(LootboxOdds.Threshold, LootboxOdds.Roll(750, () => 0.999999999));
            Assert.Equal(LootboxOdds.LowMin, LootboxOdds.Roll(750, () => 0.5) - (int)(0.5 * 126)); // untere Stufe
            var seq = new Queue<double>(new[] { 0.0, 1.0 });           // Jackpot, dann Höchstwert
            Assert.Equal(750, LootboxOdds.Roll(750, () => seq.Dequeue()));
            var seq2 = new Queue<double>(new[] { 0.0, 0.0 });
            Assert.Equal(151, LootboxOdds.Roll(100, () => seq2.Dequeue())); // RICS-Höchstwert unter 150: Jackpot trotzdem 151
            Assert.InRange(LootboxOdds.Expected(750), 100, 115);
        }
    }

    public class BetBonusTests
    {
        private const string Json = "{\"predictionId\":\"p1\",\"status\":\"RESOLVED\",\"winningOutcomeId\":\"o1\",\"outcomes\":["
            + "{\"id\":\"o1\",\"topPredictors\":[{\"userId\":\"42\",\"userLogin\":\"anna\",\"userName\":\"Anna\",\"channelPointsUsed\":500,\"channelPointsWon\":2000},"
            + "{\"userId\":\"7\",\"userLogin\":\"privo\",\"userName\":\"Privo\",\"channelPointsUsed\":100,\"channelPointsWon\":400},"
            + "{\"userId\":\"8\",\"userLogin\":\"klein\",\"userName\":\"Klein\",\"channelPointsUsed\":1,\"channelPointsWon\":1}]},"
            + "{\"id\":\"o2\",\"topPredictors\":[{\"userId\":\"9\",\"userLogin\":\"bert\",\"userName\":\"Bert\",\"channelPointsUsed\":800,\"channelPointsWon\":0}]}]}";

        [Fact]
        public void NurGewinnerMitGanzerAuszahlung()
        {
            PredictionResult r = BetBonus.Parse(Json);
            Assert.True(r.IsResolved);
            Assert.Equal(3, r.Winners.Count);                      // nur Ausgang o1
            var awards = BetBonus.Compute(r, 300, new HashSet<string> { "7" });
            BonusAward anna = Assert.Single(awards);               // privo privat, klein unter 1 Pfennig
            Assert.Equal("anna", anna.Login);
            Assert.Equal(600, anna.Pfennig);                        // 2000 Punkte = 2 h = 600
        }

        [Fact]
        public void KeinBonusBeiAbbruchOderOffen()
        {
            Assert.Empty(BetBonus.Compute(BetBonus.Parse(Json.Replace("RESOLVED", "CANCELED")), 300, null));
            Assert.Empty(BetBonus.Compute(BetBonus.Parse(Json.Replace("RESOLVED", "LOCKED")), 300, null));
            Assert.Empty(BetBonus.Compute(BetBonus.Parse("{\"status\":\"WAITING\"}"), 300, null));
            Assert.Null(BetBonus.Parse("kein json"));
        }

        [Fact]
        public void Ansage()
        {
            var awards = new List<BonusAward> { new BonusAward { Login = "anna", Pfennig = 1200 }, new BonusAward { Login = "bert", Pfennig = 90 } };
            Assert.Equal("Wettbonus: @anna +1.200 Pfennig, @bert +90 Pfennig", BetBonus.Announcement(awards, " Pfennig "));
            Assert.Null(BetBonus.Announcement(new List<BonusAward>(), "Pfennig"));
        }

        [Fact]
        public void PrivatListe()
        {
            Assert.Equal(new[] { "1", "2" }, ServerEvents.ParseOptouts("{\"userIds\":[\"1\",\"2\",\"\"]}").OrderBy(x => x));
            Assert.Null(ServerEvents.ParseOptouts("{\"x\":1}"));
        }
    }
}
