// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Linq;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    public class StreamliningTests
    {
        private static readonly System.Collections.Generic.HashSet<string> Allowed = CommandFilter.ParseList(CommandFilter.DefaultAllowed);

        [Theory]
        [InlineData("$dosenbrot 2", false, true)]
        [InlineData("!buy beer", false, true)]
        [InlineData("!pawn", false, true)]
        [InlineData("!JoinQueue", false, true)]
        [InlineData("!bal", false, false)]
        [InlineData("!openlootbox", false, false)]
        [InlineData("!event raid", false, false)]
        [InlineData("!mypawn health", false, false)]
        [InlineData("!givecoins anna 50", true, true)]   // Moderator/Streamer dürfen alles
        [InlineData("hallo", false, false)]
        [InlineData("!", false, false)]
        public void ErlaubteBefehle(string text, bool privileged, bool expected) =>
            Assert.Equal(expected, CommandFilter.IsAllowed(text, Allowed, privileged));

        [Fact]
        public void ListeLesen()
        {
            var list = CommandFilter.ParseList("!pawn, $buy;  JoinQueue");
            Assert.Equal(new[] { "buy", "joinqueue", "pawn" }, list.OrderBy(x => x));
            Assert.Empty(CommandFilter.ParseList(null));
        }

        [Theory]
        [InlineData("!pawn human hussar female 30", "!pawn")]
        [InlineData("!PAWN", "!pawn")]
        [InlineData("$dosenbrot", "$dosenbrot")]
        public void KolonistImmerZufaellig(string text, string expected) => Assert.Equal(expected, CommandFilter.StripPawnArguments(text));

        [Fact]
        public void NurEinNeuerKolonistJe30Minuten()
        {
            var now = new DateTime(2026, 11, 21, 20, 0, 0, DateTimeKind.Utc);
            var cd = TimeSpan.FromMinutes(30);
            Assert.Equal(TimeSpan.Zero, PawnGate.Remaining(null, now, cd));
            Assert.Equal(TimeSpan.FromMinutes(20), PawnGate.Remaining(now.AddMinutes(-10), now, cd));
            Assert.Equal(TimeSpan.Zero, PawnGate.Remaining(now.AddMinutes(-31), now, cd));
            Assert.Equal("Der nächste neue Kolonist ist in 20 min möglich.", PawnGate.Message(TimeSpan.FromMinutes(19.2)));
            Assert.Equal("Der nächste neue Kolonist ist in 1 min möglich.", PawnGate.Message(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void EineEinblendungJeBefehl()
        {
            string s = ReplyComposer.Compose("anna",
                new[] { "@anna 🎁 Alle Lootboxen: 75 Pfennig!", "@anna Dosenbrot 2 für 240, per Rimazon geliefert. Rest: 35", "@anna" },
                "Guthaben: 35 Pfennig · Karma 🙂");
            Assert.Equal("@anna: 🎁 Alle Lootboxen: 75 Pfennig! · Dosenbrot 2 für 240, per Rimazon geliefert. Rest: 35 · Guthaben: 35 Pfennig · Karma 🙂", s);
            Assert.Null(ReplyComposer.Compose("anna", new string[0], null));
            Assert.Equal("@anna: Guthaben: 5 Pfennig", ReplyComposer.Compose("anna", null, "Guthaben: 5 Pfennig"));
        }

        [Fact]
        public void LadenAuswahl()
        {
            Assert.Equal(6, ShopSelection.Default.Count);
            Assert.Equal(new[] { "Dosenbrot", "Rausch", "Ei", "Kartoffeln", "Flausch", "Holz vor der Hütte" }, ShopSelection.Default.Select(e => e.CustomName));
            Assert.All(ShopSelection.Default, e => Assert.True(e.MaxPerPurchase > 0));
        }
    }
}
