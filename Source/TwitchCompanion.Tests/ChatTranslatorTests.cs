// SPDX-License-Identifier: AGPL-3.0-or-later
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    public class ChatTranslatorTests
    {
        [Theory]
        [InlineData("@anna Welcome to the stream! You currently have 1 Lootbox(es) to open. Use !openlootbox", "@anna Willkommen! 1× Lootbox offen: !openlootbox")]
        [InlineData("@bert You don't have permission to use !givecoins. Required: moderator", "@bert !givecoins ist nur für Moderatoren.")]
        [InlineData("@bert Command is on cooldown. Try again in 12 seconds.", "@bert Noch 12 s warten.")]
        [InlineData("@bert Command is on cooldown.", "@bert Gerade gesperrt, bitte kurz warten.")]
        [InlineData("✨ Anna just got a fresh new name! Welcome back!", "✨ Anna hat einen neuen Namen. Willkommen zurück!")]
        [InlineData("@anna Guthaben: 100 Pfennig", "@anna Guthaben: 100 Pfennig")]
        public void FesteTexte(string en, string de) => Assert.Equal(de, ChatTranslator.Translate(en));

        [Fact]
        public void ZusammengefassteZeilenWerdenEinzelnUebersetzt()
        {
            string merged = "@a Welcome to the stream! You currently have 2 Lootbox(es) to open. Use !openlootbox";
            Assert.StartsWith("@a Willkommen! 2× Lootbox offen", ChatTranslator.Translate(merged));
        }

        [Theory]
        [InlineData(100, " Pfennig ", "🙂", 1, "1× Lootbox offen · Guthaben: 100 Pfennig · Karma 🙂")]
        [InlineData(12345, "Pfennig", "😇", 0, "Guthaben: 12.345 Pfennig · Karma 😇")]
        [InlineData(5, null, null, 0, "Guthaben: 5 Pfennig")]
        public void KurzesGuthaben(int coins, string currency, string emoji, int boxes, string expected)
            => Assert.Equal(expected, ChatTranslator.Balance(coins, currency, emoji, boxes));
    }
}
