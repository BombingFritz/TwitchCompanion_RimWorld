// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Befehle und Coins nur in der Twitch-Kategorie RimWorld.

using System;
using System.Collections.Generic;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>
    /// Regel des Nutzers (19.11.2026): Befehle und Coins nur, solange der Kanal in der Kategorie RimWorld ist
    /// (Spec, Stufe 1, „Zuschauer und Coins“). Kategorie unbekannt oder Server ohne die Fähigkeit: gesperrt,
    /// außer die Prüfung ist in den Einstellungen ausgeschaltet.
    /// </summary>
    public static class CategoryGate
    {
        public const string RimWorldCategory = "RimWorld";

        public static bool IsRimWorld(string category)
            => !string.IsNullOrWhiteSpace(category)
               && string.Equals(category.Trim(), RimWorldCategory, StringComparison.OrdinalIgnoreCase);

        /// <summary>Darf die Brücke gerade Befehle weiterleiten und Aktivität auffrischen?</summary>
        public static bool IsOpen(bool connected, bool requireCategory, ChannelInfo channel)
        {
            if (!connected) return false;
            if (!requireCategory) return true;
            return channel != null && IsRimWorld(channel.Category);
        }

        /// <summary>
        /// Wählt die Chatter, deren Aktivität RICS auffrischen soll: nur Nachrichten, seit die Kategorie RimWorld ist
        /// (sonst bekämen Zuschauer aus „Just Chatting“ nach dem Wechsel Coins), und nicht aus der Zukunft.
        /// </summary>
        public static List<ActiveChatter> SelectForRefresh(IEnumerable<ActiveChatter> chatters, bool requireCategory,
            ChannelInfo channel, DateTime nowUtc)
        {
            var result = new List<ActiveChatter>();
            if (chatters == null) return result;
            DateTime? since = requireCategory ? channel?.CategoryChangedAt : null;
            foreach (var c in chatters)
            {
                if (c == null) continue;
                if (since.HasValue && c.LastChatAt < since.Value) continue;
                if (c.LastChatAt > nowUtc.AddMinutes(1)) continue;
                result.Add(c);
            }
            return result;
        }
    }

    /// <summary>Merkt sich die letzten Nachrichten-IDs, um doppelt zugestellte Ereignisse zu verwerfen. Thread-sicher.</summary>
    public sealed class RecentIds
    {
        private readonly int _capacity;
        private readonly HashSet<string> _set = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>();
        private readonly object _lock = new object();

        public RecentIds(int capacity = 500)
        {
            _capacity = Math.Max(1, capacity);
        }

        /// <summary>Wahr beim ersten Mal; leere IDs gelten immer als neu.</summary>
        public bool TryAdd(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            lock (_lock)
            {
                if (!_set.Add(id)) return false;
                _order.Enqueue(id);
                while (_order.Count > _capacity) _set.Remove(_order.Dequeue());
                return true;
            }
        }
    }
}
