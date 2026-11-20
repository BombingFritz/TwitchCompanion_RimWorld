// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – deutsche, kurze Fassungen der fest eingebauten englischen RICS-Texte.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>
    /// RICS übersetzt die meisten Chattexte über RimWorld-Sprachdateien (deutsche Fassung in <c>Languages/</c> dieser Mod).
    /// Einige Texte stehen fest im Code von RICS 1.56; die schreibt die Mod beim Senden um (Nutzer, 20.11.2026: kurz und
    /// auf Deutsch, Ausführliches auf der Webseite).
    /// </summary>
    public static class ChatTranslator
    {
        private static readonly List<KeyValuePair<Regex, string>> Rules = new List<KeyValuePair<Regex, string>>
        {
            Rule(@"Welcome to the stream! You currently have (\d+) Lootbox\(es\) to open\. Use !openlootbox", "Willkommen! $1× Lootbox offen: !openlootbox"),
            Rule(@"You don't have permission to use (\S+)\. Required: (\w+)", "$1 ist nur für ${2}."),
            Rule(@"Command is on cooldown\. Try again in (\d+) seconds\.", "Noch $1 s warten."),
            Rule(@"Command is on cooldown\.", "Gerade gesperrt, bitte kurz warten."),
            Rule(@"✨ (.+) just got a fresh new name! Welcome back!", "✨ $1 hat einen neuen Namen. Willkommen zurück!"),
            Rule(@"Welcome to the stream, @(\S+)! Type !help for available commands\.", "Willkommen, @$1! Alles zum Mitspielen: !help"),
        };

        private static readonly Dictionary<string, string> Roles = new Dictionary<string, string>
        {
            { "moderator", "Moderatoren" }, { "broadcaster", "den Streamer" }, { "vip", "VIPs" }, { "subscriber", "Abonnenten" }, { "everyone", "alle" },
        };

        private static KeyValuePair<Regex, string> Rule(string pattern, string replacement) =>
            new KeyValuePair<Regex, string>(new Regex(pattern, RegexOptions.CultureInvariant), replacement);

        public static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var rule in Rules)
                text = rule.Key.Replace(text, rule.Value);
            return Regex.Replace(text, @"ist nur für (\w+)\.", m =>
                "ist nur für " + (Roles.TryGetValue(m.Groups[1].Value.ToLowerInvariant(), out string r) ? r : m.Groups[1].Value) + ".");
        }

        /// <summary>Kurze Antwort auf <c>!bal</c>: „1× Lootbox offen · Guthaben: 100 Pfennig · Karma 🙂“.</summary>
        public static string Balance(int coins, string currency, string karmaEmoji, int lootboxes)
        {
            string unit = string.IsNullOrWhiteSpace(currency) ? "Pfennig" : currency.Trim();
            string money = "Guthaben: " + coins.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " " + unit
                + (string.IsNullOrEmpty(karmaEmoji) ? string.Empty : " · Karma " + karmaEmoji);
            return lootboxes > 0 ? lootboxes + "× Lootbox offen · " + money : money;
        }
    }
}
