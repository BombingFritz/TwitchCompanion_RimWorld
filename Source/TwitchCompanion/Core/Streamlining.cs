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
        /// <summary>Vorgabe: kaufen, neuer Kolonist, anstellen, Kolonist freigeben, Raid-Beitritt (Nutzer, 20.11.2026).</summary>
        public const string DefaultAllowed = "buy, pawn, joinqueue, leave, joinraid";

        /// <summary>Vorgabe bis 20.11.2026; gespeicherte Einstellungen mit genau diesem Wert bekommen die neue Vorgabe.</summary>
        public const string PreviousDefault = "buy, pawn, joinqueue, leavequeue, queuestatus, acceptpawn, leave, joinraid";

        public static string Migrate(string saved) => saved == null || saved == PreviousDefault ? DefaultAllowed : saved;

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
            new ShopEntry("Chocolate", "Schoki", 5),
            new ShopEntry("MedicineHerbal", "Hausmittel", 3),
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

    /// <summary>
    /// Warteschlange zum Übernehmen (Nutzer, 20.11.2026): Wer sich zuletzt anstellt, steht vorne; anstellen nur einmal je
    /// Stream; nach dem Stream wird die Liste geleert.
    /// </summary>
    public static class QueueRules
    {
        /// <summary>
        /// So lange darf der Stream offline sein, bevor er als beendet gilt (Absturz, kurze Unterbrechung). Vorschlag von Claude.
        /// </summary>
        public static readonly TimeSpan StreamGap = TimeSpan.FromMinutes(30);

        public const string AlreadyJoined = "Du hast dich in diesem Stream schon angestellt.";

        /// <summary>
        /// Verfolgt, ob der Stream läuft. Wahr, wenn die Warteschlange geleert werden soll: ein neuer Stream beginnt
        /// (live nach mehr als <see cref="StreamGap"/> Pause oder zum ersten Mal) oder ein Stream ist seit mehr als
        /// <see cref="StreamGap"/> zu Ende.
        /// </summary>
        public static bool StreamChanged(bool live, DateTime nowUtc, ref DateTime? lastLiveUtc)
        {
            if (live)
            {
                bool newStream = lastLiveUtc == null || nowUtc - lastLiveUtc.Value > StreamGap;
                lastLiveUtc = nowUtc;
                return newStream;
            }
            if (lastLiveUtc != null && nowUtc - lastLiveUtc.Value > StreamGap)
            {
                lastLiveUtc = null;
                return true;
            }
            return false;
        }

        /// <summary>RICS hängt neue Einträge hinten an; die Mod holt den zuletzt angehängten nach vorne.</summary>
        public static void MoveLastToFront<T>(IList<T> queue)
        {
            if (queue == null || queue.Count < 2) return;
            T last = queue[queue.Count - 1];
            queue.RemoveAt(queue.Count - 1);
            queue.Insert(0, last);
        }
    }

    /// <summary>Freigegebene Kolonisten heißen „Dulli“, „Dulli 2“, … (Nutzer, 20.11.2026).</summary>
    public static class DulliNames
    {
        public const string Base = "Dulli";

        public static string Next(IEnumerable<string> usedNicks)
        {
            var used = new HashSet<string>((usedNicks ?? Enumerable.Empty<string>()).Where(n => n != null).Select(n => n.Trim()),
                StringComparer.OrdinalIgnoreCase);
            if (!used.Contains(Base)) return Base;
            for (int i = 2; ; i++)
                if (!used.Contains(Base + " " + i)) return Base + " " + i;
        }
    }

    /// <summary>
    /// Nach <c>!leave</c>: bleibt als „Dulli“ in der Kolonie, oder verlässt sie unter dem Namen des Zuschauers
    /// (wie verbannt). Später soll der Chat abstimmen (Nutzer, 20.11.2026; blockiert durch Server-Issue #81).
    /// </summary>
    public enum LeaveMode { StaysAsDulli, Departs }

    public enum SpamVerdict { Ok, Strike, Ignored }

    /// <summary>
    /// Spamschutz (Nutzer, 20.11.2026): mehr als 5 Befehle in einer Minute oder mehr als 10 in 10 Minuten geben eine
    /// Verwarnung; nach 3 Verwarnungen ignoriert der Bot den Zuschauer bis Streamende. Nach einer Verwarnung zählt die
    /// Mod von vorn, damit ein einzelner Schwall nur eine Verwarnung gibt.
    /// </summary>
    public sealed class SpamGuard
    {
        public const int MaxStrikes = 3;
        public static readonly (int Count, TimeSpan Window)[] Limits = { (5, TimeSpan.FromMinutes(1)), (10, TimeSpan.FromMinutes(10)) };

        private readonly Dictionary<string, List<DateTime>> _recent = new Dictionary<string, List<DateTime>>();
        public Dictionary<string, int> Strikes { get; } = new Dictionary<string, int>();

        public int StrikesOf(string userId) => userId != null && Strikes.TryGetValue(userId, out int n) ? n : 0;
        public bool IsIgnored(string userId) => StrikesOf(userId) >= MaxStrikes;

        public SpamVerdict Record(string userId, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(userId)) return SpamVerdict.Ok;
            if (IsIgnored(userId)) return SpamVerdict.Ignored;
            if (!_recent.TryGetValue(userId, out List<DateTime> times)) _recent[userId] = times = new List<DateTime>();
            TimeSpan longest = Limits.Max(l => l.Window);
            times.RemoveAll(t => nowUtc - t >= longest);
            times.Add(nowUtc);
            if (!Limits.Any(l => times.Count(t => nowUtc - t < l.Window) > l.Count)) return SpamVerdict.Ok;
            times.Clear();
            Strikes[userId] = StrikesOf(userId) + 1;
            return SpamVerdict.Strike;
        }

        public void Reset()
        {
            _recent.Clear();
            Strikes.Clear();
        }

        /// <summary>Speicherform „userId=Anzahl“ (nur IDs, keine Namen).</summary>
        public List<string> Save() => Strikes.Select(kv => kv.Key + "=" + kv.Value).ToList();

        public void Load(IEnumerable<string> saved)
        {
            Strikes.Clear();
            foreach (string e in saved ?? Enumerable.Empty<string>())
            {
                int eq = e?.LastIndexOf('=') ?? -1;
                if (eq > 0 && int.TryParse(e.Substring(eq + 1), out int n) && n > 0) Strikes[e.Substring(0, eq)] = n;
            }
        }

        public static string Message(int strikes) => strikes >= MaxStrikes
            ? "Dritte Verwarnung: Der Bot ignoriert dich bis zum Ende des Streams."
            : "Bitte nicht spammen – Verwarnung " + strikes + " von " + MaxStrikes + ". Bei " + MaxStrikes + " ignoriert dich der Bot bis Streamende.";
    }
}

