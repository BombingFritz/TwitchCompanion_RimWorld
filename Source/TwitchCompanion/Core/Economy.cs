// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Preise in Zuschauzeit, Lootbox-Chancen (reine Rechnung, ohne RimWorld).

using System;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Regeln für das Preis-Werkzeug (Feature-Issue #10, Entscheidungen des Nutzers vom 20.11.2026).</summary>
    public sealed class PricePlan
    {
        /// <summary>Faktor auf den Grundpreis von RICS (≈ Marktwert); Bier ≈ 12 → 120 ≈ 24 min.</summary>
        public float ItemFactor = 10f;
        /// <summary>Tiere kosten mindestens so viele Zuschaustunden.</summary>
        public float AnimalMinHours = 1f;
        /// <summary>Neuer Kolonist (Rasse bzw. Xenotyp „Baseliner“) in Zuschaustunden.</summary>
        public float NewPawnHours = 2f;
        /// <summary>Spargel, falls ein Mod ihn bringt, in Zuschauminuten je Stück.</summary>
        public float AsparagusMinutes = 10f;
    }

    public static class Economy
    {
        /// <summary>RICS zahlt alle 2 Minuten aus.</summary>
        public const int AwardsPerHour = 30;

        /// <summary>Pfennig je Stunde für einen aktiven Zuschauer ohne Boni (Karma 100).</summary>
        public static int CoinsPerHour(int baseCoinReward) => Math.Max(1, baseCoinReward) * AwardsPerHour;

        public static int HoursToCoins(float hours, int coinsPerHour) => Math.Max(1, (int)Math.Round(hours * coinsPerHour));

        public static int ScaledItemPrice(int defaultPrice, float factor) => Math.Max(1, (int)Math.Round(Math.Max(1, defaultPrice) * factor));

        /// <summary>Behält das Verhältnis eines Preises zum alten Grundpreis bei (z. B. Xenotypen zur Rasse).</summary>
        public static int ScaleRelative(float price, float oldBase, int newBase)
        {
            if (oldBase <= 0 || price <= 0) return newBase;
            return Math.Max(1, (int)Math.Round(price / oldBase * newBase));
        }

        public static bool IsAsparagus(string defName, string label)
        {
            bool Has(string s) => !string.IsNullOrEmpty(s)
                && (s.IndexOf("asparagus", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("spargel", StringComparison.OrdinalIgnoreCase) >= 0);
            return Has(defName) || Has(label);
        }
    }

    /// <summary>
    /// Pfennig aus einer Lootbox: meist bis <see cref="Threshold"/>, nur mit <see cref="JackpotChance"/> (1 zu 20) mehr,
    /// dann bis zum Höchstwert aus RICS (Nutzer, 20.11.2026).
    /// </summary>
    public static class LootboxOdds
    {
        public const int Threshold = 150;
        public const int LowMin = 25;
        public const double JackpotChance = 0.05;

        /// <param name="rangeMax">Höchstwert aus den RICS-Einstellungen (Vorgabe 750).</param>
        /// <param name="next01">Zufallszahl in [0, 1).</param>
        public static int Roll(int rangeMax, Func<double> next01)
        {
            Func<double> rnd = () => Math.Min(Math.Max(next01(), 0), 0.999999);
            int highMax = Math.Max(rangeMax, Threshold + 1);
            if (rnd() < JackpotChance)
                return Threshold + 1 + (int)(rnd() * (highMax - Threshold));
            return LowMin + (int)(rnd() * (Threshold - LowMin + 1));
        }

        /// <summary>Erwartungswert einer Lootbox, für die Anzeige.</summary>
        public static double Expected(int rangeMax)
        {
            int highMax = Math.Max(rangeMax, Threshold + 1);
            return (1 - JackpotChance) * (LowMin + Threshold) / 2.0 + JackpotChance * (Threshold + 1 + highMax) / 2.0;
        }
    }
}
