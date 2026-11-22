// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Karma in zwei Ebenen (Nutzer, 22.11.2026; Feature-Issue #24):
// Karma je Zuschauer (das RICS-Karma, 0 bis 200) und ein laufendes Karma der Kolonie (−100 bis +100).

using System;
using System.Collections.Generic;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Was ein Zuschauer oder der Chat ausgelöst hat.</summary>
    public enum KarmaAction
    {
        ItemPurchase,   // Ware für die Kolonie gekauft
        PawnPurchase,   // neuer Kolonist
        RabbitPurchase, // Killer-Kaninchen gekauft
        RabbitJoin,     // beim Kaninchen-Angriff mitgemacht
        RaidJoin,       // mit !joinraid einem feindlichen Twitch-Raid beigetreten
        VoteGood, VoteNeutral, VoteBad, VoteDoom, VoteBlessing,
    }

    /// <summary>
    /// Wie stark eine Aktion das Karma verändert (Vorschlag von Claude, 22.11.2026). Zuschauer: RICS-Skala 0 bis 200.
    /// Kolonie: −100 bis +100; Angriffe zählen je Angreifer.
    /// </summary>
    public static class KarmaRules
    {
        public static float ViewerDelta(KarmaAction a)
        {
            switch (a)
            {
                case KarmaAction.ItemPurchase: return 2f;
                case KarmaAction.RabbitPurchase: return -20f;
                case KarmaAction.RabbitJoin: return -10f;
                case KarmaAction.RaidJoin: return -10f;
                default: return 0f;
            }
        }

        public static float ColonyDelta(KarmaAction a, int count = 1)
        {
            switch (a)
            {
                case KarmaAction.ItemPurchase: return 1f;
                case KarmaAction.PawnPurchase: return 3f;
                case KarmaAction.RabbitPurchase: return -2f * Math.Max(0, count); // je angreifendes Kaninchen (beim Kauf 0)
                case KarmaAction.RaidJoin: return -2f * Math.Max(0, count);       // je Raider eines feindlichen Twitch-Raids (beim Beitritt 0)
                case KarmaAction.VoteGood: return 5f;
                case KarmaAction.VoteBad: return -5f;
                case KarmaAction.VoteDoom: return -15f;
                case KarmaAction.VoteBlessing: return 15f;
                default: return 0f;
            }
        }

        public static KarmaAction ForVote(EventKarma k, bool calm) =>
            calm ? KarmaAction.VoteNeutral
            : k == EventKarma.Good ? KarmaAction.VoteGood
            : k == EventKarma.Bad ? KarmaAction.VoteBad
            : k == EventKarma.Doom ? KarmaAction.VoteDoom
            : k == EventKarma.Blessing ? KarmaAction.VoteBlessing
            : KarmaAction.VoteNeutral;
    }

    /// <summary>Laufendes Karma der Kolonie; klingt mit einer Halbwertszeit gegen 0 ab, damit es die jüngere Geschichte spiegelt.</summary>
    public sealed class ColonyKarma
    {
        public const float Min = -100f, Max = 100f;
        public float Value;
        public DateTime? LastUtc;

        public void Decay(DateTime nowUtc, TimeSpan halfLife)
        {
            if (LastUtc.HasValue && halfLife > TimeSpan.Zero && nowUtc > LastUtc.Value)
                Value *= (float)Math.Pow(0.5, (nowUtc - LastUtc.Value).TotalMinutes / halfLife.TotalMinutes);
            if (Math.Abs(Value) < 0.05f) Value = 0f;
            LastUtc = nowUtc;
        }

        public void Add(float delta, DateTime nowUtc, TimeSpan halfLife)
        {
            Decay(nowUtc, halfLife);
            Value = Math.Max(Min, Math.Min(Max, Value + delta));
        }

        public static string Word(float v) =>
            v >= 60 ? "strahlend" : v >= 25 ? "gut" : v > -25 ? "ausgeglichen" : v > -60 ? "schlecht" : "finster";
    }

    public enum JusticeKind { None, Doom, Blessing }

    /// <summary>
    /// Ausgleichende Gerechtigkeit (Nutzer, 19. und 22.11.2026): Ist das Karma der Kolonie sehr gut, kann in der Abstimmung
    /// etwas Verheerendes zur Wahl stehen; ist es sehr schlecht, etwas extrem Gutes. Die Wahrscheinlichkeit wächst ab der
    /// Schwelle linear bis zur Höchstchance bei ±100.
    /// </summary>
    public static class Justice
    {
        public static double Chance(float colony, float threshold, double maxChance)
        {
            float a = Math.Abs(colony);
            if (a < threshold || threshold >= ColonyKarma.Max) return 0;
            return Math.Min(1.0, maxChance * (a - threshold) / (ColonyKarma.Max - threshold));
        }

        public static JusticeKind Roll(float colony, float threshold, double maxChance, Random rng)
        {
            if (rng.NextDouble() >= Chance(colony, threshold, maxChance)) return JusticeKind.None;
            return colony > 0 ? JusticeKind.Doom : JusticeKind.Blessing;
        }
    }

    public enum PawnFate { Cursed, Unlucky, Normal, Blessed }

    /// <summary>Wie das Karma des Käufers einen neuen Kolonisten prägt (Nutzer, 22.11.2026; Schwellen Vorschlag von Claude).</summary>
    public static class PawnFates
    {
        public static PawnFate For(float viewerKarma) =>
            viewerKarma < 40f ? PawnFate.Cursed : viewerKarma < 70f ? PawnFate.Unlucky : viewerKarma >= 160f ? PawnFate.Blessed : PawnFate.Normal;

        /// <summary>Schlechte Eigenschaften (DefName, Grad), aus denen eine gewählt wird.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, int>> BadTraits = new[]
        {
            new KeyValuePair<string, int>("Psychopath", 0), new KeyValuePair<string, int>("Pyromaniac", 0),
            new KeyValuePair<string, int>("Wimp", 0), new KeyValuePair<string, int>("SlowLearner", 0),
            new KeyValuePair<string, int>("Industriousness", -2), new KeyValuePair<string, int>("NaturalMood", -2),
            new KeyValuePair<string, int>("Nerves", -2), new KeyValuePair<string, int>("Abrasive", 0),
        };

        public static readonly IReadOnlyList<KeyValuePair<string, int>> GoodTraits = new[]
        {
            new KeyValuePair<string, int>("Industriousness", 2), new KeyValuePair<string, int>("Tough", 0),
            new KeyValuePair<string, int>("FastLearner", 0), new KeyValuePair<string, int>("NaturalMood", 2),
            new KeyValuePair<string, int>("Nerves", 2),
        };

        /// <summary>Schwere Krankheiten für „verflucht“ (HediffDef-Namen; die erste vorhandene wird genommen, gemischt).</summary>
        public static readonly IReadOnlyList<string> Diseases = new[] { "Plague", "Malaria", "Flu" };
    }
}
