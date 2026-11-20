// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ausmisten (Nutzer, 20.11.2026): wenige Befehle, kleiner Laden, Antworten im Spiel.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Welche Befehle Zuschauer nutzen dürfen; Moderatoren und Streamer dürfen alles.</summary>
    public static class CommandFilter
    {
        /// <summary>Vorgabe: kaufen, neuer Kolonist, Warteschlange, Kolonist freigeben, Raid-Beitritt.</summary>
        public const string DefaultAllowed = "buy, pawn, joinqueue, leavequeue, queuestatus, acceptpawn, leave, joinraid";

        public static HashSet<string> ParseList(string list) =>
            new HashSet<string>((list ?? string.Empty).Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().TrimStart('!', '$').ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

        /// <summary>Befehlswort ohne Präfix, klein; Käufe mit <c>$</c> zählen als <c>buy</c>; sonst <c>null</c>.</summary>
        public static string CommandWord(string text, string prefix = "!", string buyPrefix = "$")
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string first = text.TrimStart().Split(new[] { ' ', '\t' }, 2)[0];
            if (!string.IsNullOrEmpty(buyPrefix) && first.StartsWith(buyPrefix, StringComparison.Ordinal)) return "buy";
            if (!string.IsNullOrEmpty(prefix) && first.StartsWith(prefix, StringComparison.Ordinal) && first.Length > prefix.Length)
                return first.Substring(prefix.Length).ToLowerInvariant();
            return null;
        }

        public static bool IsAllowed(string text, ICollection<string> allowed, bool privileged, string prefix = "!", string buyPrefix = "$")
        {
            if (privileged) return true;
            string word = CommandWord(text, prefix, buyPrefix);
            return word != null && allowed != null && allowed.Contains(word);
        }

        /// <summary>Neue Kolonisten sind immer zufällig: <c>!pawn …</c> wird zu <c>!pawn</c>.</summary>
        public static string StripPawnArguments(string text, string prefix = "!") =>
            CommandWord(text, prefix) == "pawn" ? prefix + "pawn" : text;
    }

    /// <summary>Höchstens ein neuer Kolonist je Zeitraum, für den ganzen Stream.</summary>
    public static class PawnGate
    {
        /// <summary>Restzeit bis zum nächsten erlaubten Kauf; <see cref="TimeSpan.Zero"/>, wenn frei.</summary>
        public static TimeSpan Remaining(DateTime? lastPurchaseUtc, DateTime nowUtc, TimeSpan cooldown)
        {
            if (lastPurchaseUtc == null) return TimeSpan.Zero;
            TimeSpan left = lastPurchaseUtc.Value + cooldown - nowUtc;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        public static string Message(TimeSpan remaining) =>
            "Der nächste neue Kolonist ist in " + Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)) + " min möglich.";
    }

    /// <summary>Kleiner Laden (Nutzer, 20.11.2026): nur diese Items, mit eigenen Namen und Mengengrenze je Kauf.</summary>
    public sealed class ShopEntry
    {
        public string DefName;
        public string CustomName;
        public int MaxPerPurchase;
        public ShopEntry(string defName, string customName, int maxPerPurchase) { DefName = defName; CustomName = customName; MaxPerPurchase = maxPerPurchase; }
    }

    public static class ShopSelection
    {
        public static readonly IReadOnlyList<ShopEntry> Default = new[]
        {
            new ShopEntry("Beer", "Dosenbrot", 3),
            new ShopEntry("SmokeleafJoint", "Rausch", 2),
            new ShopEntry("EggChickenUnfertilized", "Ei", 12),
            new ShopEntry("RawPotatoes", "Kartoffeln", 30),
            new ShopEntry("WoolAlpaca", "Flausch", 25),
            new ShopEntry("WoodLog", "Holz", 50),
        };
    }

    /// <summary>
    /// Eine Einblendung je Befehl: „@anna: 🎁 Lootbox +75 · 2× Dosenbrot … · Guthaben: 175 Pfennig“.
    /// RICS setzt vor jede Antwort „@login “; das fällt hier weg.
    /// </summary>
    public static class ReplyComposer
    {
        public static string Compose(string login, IEnumerable<string> parts, string balance)
        {
            string at = "@" + login;
            var clean = new List<string>();
            foreach (string p in parts ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                string t = p.Trim();
                if (t.StartsWith(at + " ", StringComparison.OrdinalIgnoreCase)) t = t.Substring(at.Length + 1).Trim();
                else if (t.Equals(at, StringComparison.OrdinalIgnoreCase)) continue;
                if (t.Length > 0 && !clean.Contains(t)) clean.Add(t);
            }
            if (!string.IsNullOrWhiteSpace(balance)) clean.Add(balance.Trim());
            return clean.Count == 0 ? null : at + ": " + string.Join(" · ", clean);
        }
    }
}
