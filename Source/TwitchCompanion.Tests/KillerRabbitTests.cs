// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Linq;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    public class KillerRabbitTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 22, 20, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData("$Killer-Kaninchen", true)]
        [InlineData("$killerkaninchen", true)]
        [InlineData("$Kaninchen", true)]
        [InlineData("$ kaninchen", true)]
        [InlineData("!buy Killer-Kaninchen", true)]
        [InlineData("$Kartoffeln 10", false)]
        [InlineData("!pawn", false)]
        [InlineData("Kaninchen", false)]
        [InlineData("", false)]
        public void KaufErkennen(string text, bool expected) => Assert.Equal(expected, KillerRabbits.IsPurchase(text));

        [Fact]
        public void PreisIstDieHaelfteEinesKolonisten()
        {
            Assert.Equal(300, KillerRabbits.Price(600, 0.5f));
            Assert.Equal(1, KillerRabbits.Price(0, 0.5f));
        }

        [Theory]
        [InlineData(88f, 1f, 25f, 10, 3)]
        [InlineData(88f, 1f, 25f, 2, 2)]    // nie mehr als Namen
        [InlineData(10f, 1f, 25f, 10, 1)]   // der Käufer immer
        [InlineData(500f, 2f, 25f, 100, 40)]
        public void Begrenzung(float points, float factor, float power, int names, int expected) =>
            Assert.Equal(expected, KillerRabbits.MaxRabbits(points, factor, power, names));

        [Fact]
        public void BeitrittNurEinmalUndNurImFenster()
        {
            var a = new RabbitAttack("1", "Anna", T0, TimeSpan.FromSeconds(90));
            Assert.True(a.Join("2", "Bert", T0.AddSeconds(10)));
            Assert.False(a.Join("2", "Bert", T0.AddSeconds(11)));
            Assert.False(a.Join("1", "Anna", T0.AddSeconds(12)));     // Käufer ist schon dabei
            Assert.False(a.Join("3", "Cleo", T0.AddSeconds(90)));     // Fenster zu
            Assert.Equal(2, a.Count);
            Assert.False(a.IsOpen(T0.AddSeconds(90)));
        }

        [Fact]
        public void KaeuferGreiftImmerAn()
        {
            var a = new RabbitAttack("1", "Anna", T0, TimeSpan.FromSeconds(90));
            for (int i = 2; i < 10; i++) a.Join(i.ToString(), "Z" + i, T0);
            for (int seed = 0; seed < 20; seed++)
            {
                var attackers = a.Attackers(3, new Random(seed));
                Assert.Equal(3, attackers.Count);
                Assert.Equal("Anna", attackers[0]);
            }
        }

        [Fact]
        public void Abstand()
        {
            Assert.Equal(TimeSpan.Zero, KillerRabbits.Cooldown(null, T0, TimeSpan.FromMinutes(20)));
            Assert.Equal(TimeSpan.FromMinutes(5), KillerRabbits.Cooldown(T0, T0.AddMinutes(15), TimeSpan.FromMinutes(20)));
        }
    }
}
