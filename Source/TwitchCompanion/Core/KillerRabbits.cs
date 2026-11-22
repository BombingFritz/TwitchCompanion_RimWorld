// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Killer-Kaninchen (Nutzer, 22.11.2026): ein kaufbarer Angriff wie ein kleiner Raid.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>
    /// Ein Zuschauer kauft ein Killer-Kaninchen, das seinen Namen trägt und die Kolonie angreift. Wie beim Raid können
    /// andere mit <c>!joinraid</c> als weitere Kaninchen mitkommen; wie viele angreifen, begrenzt die Bedrohung der Kolonie.
    /// </summary>
    public static class KillerRabbits
    {
        /// <summary>Wörter nach dem Kaufpräfix, die das Killer-Kaninchen meinen (Groß/klein egal).</summary>
        public static readonly string[] Names = { "killer-kaninchen", "killerkaninchen", "kaninchen" };

        public const string DisplayName = "Killer-Kaninchen";

        /// <summary>Ist <paramref name="text"/> ein Kauf des Killer-Kaninchens (<c>$Killer-Kaninchen</c>, auch <c>!buy kaninchen</c>)?</summary>
        public static bool IsPurchase(string text, string prefix = "!", string buyPrefix = "$")
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] parts = text.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string item;
            if (!string.IsNullOrEmpty(buyPrefix) && parts[0].StartsWith(buyPrefix, StringComparison.Ordinal))
                item = parts[0].Length > buyPrefix.Length ? parts[0].Substring(buyPrefix.Length) : (parts.Length > 1 ? parts[1] : null);
            else if (CommandFilter.CommandWord(text, prefix, buyPrefix) == "buy" && parts.Length > 1)
                item = parts[1];
            else return false;
            return item != null && Names.Contains(item.ToLowerInvariant());
        }

        /// <summary>Preis: ein Anteil des Preises für einen neuen Kolonisten (Vorgabe die Hälfte), mindestens 1.</summary>
        public static int Price(int pawnPrice, float share) => Math.Max(1, (int)Math.Round(Math.Max(0, pawnPrice) * Math.Max(0f, share)));

        /// <summary>
        /// Wie viele Kaninchen angreifen: Bedrohungspunkte × Faktor / Kampfkraft eines Kaninchens, mindestens eins (der Käufer),
        /// höchstens so viele, wie Namen da sind.
        /// </summary>
        public static int MaxRabbits(float threatPoints, float strengthFactor, float rabbitCombatPower, int names)
        {
            float power = Math.Max(1f, rabbitCombatPower);
            int byPoints = (int)Math.Floor(Math.Max(0f, threatPoints) * Math.Max(0f, strengthFactor) / power);
            return Math.Max(1, Math.Min(Math.Max(1, names), byPoints));
        }

        /// <summary>Wartezeit bis zum nächsten Kauf; <see cref="TimeSpan.Zero"/>, wenn frei.</summary>
        public static TimeSpan Cooldown(DateTime? lastAttackUtc, DateTime nowUtc, TimeSpan gap) => PawnGate.Remaining(lastAttackUtc, nowUtc, gap);
    }

    /// <summary>Ein laufender Angriff: Käufer, Beitretende und Ende des Beitrittsfensters.</summary>
    public sealed class RabbitAttack
    {
        public readonly string BuyerUserId;
        public readonly string BuyerName;
        public readonly DateTime OpenUntilUtc;
        private readonly List<KeyValuePair<string, string>> _members = new List<KeyValuePair<string, string>>();

        public RabbitAttack(string buyerUserId, string buyerName, DateTime nowUtc, TimeSpan window)
        {
            BuyerUserId = buyerUserId;
            BuyerName = buyerName;
            OpenUntilUtc = nowUtc + window;
            _members.Add(new KeyValuePair<string, string>(buyerUserId ?? buyerName, buyerName));
        }

        public int Count => _members.Count;
        public IEnumerable<string> MemberNames => _members.Select(m => m.Value);
        public bool IsOpen(DateTime nowUtc) => nowUtc < OpenUntilUtc;

        /// <summary>Fügt einen Zuschauer hinzu; falsch, wenn er schon dabei ist oder das Fenster zu ist.</summary>
        public bool Join(string userId, string name, DateTime nowUtc)
        {
            if (!IsOpen(nowUtc) || string.IsNullOrWhiteSpace(name)) return false;
            string key = string.IsNullOrEmpty(userId) ? name : userId;
            if (_members.Any(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(m.Value, name, StringComparison.OrdinalIgnoreCase))) return false;
            _members.Add(new KeyValuePair<string, string>(key, name));
            return true;
        }

        /// <summary>Die Angreifer: der Käufer immer, die übrigen ausgelost, höchstens <paramref name="max"/>.</summary>
        public List<string> Attackers(int max, Random rng) => RaidJoiners.Trim(MemberNames.ToList(), Math.Max(1, max), rng);
    }
}
