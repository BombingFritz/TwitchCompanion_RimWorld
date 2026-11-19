// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – fasst Chatzeilen von RICS zu wenigen Chatnachrichten zusammen.

using System;
using System.Collections.Generic;
using System.Text;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>
    /// Sammelt ausgehende Zeilen. Zeilen, die in kurzem Abstand kommen, werden mit „ · “ zu einer Nachricht
    /// bis <see cref="MaxMessageLength"/> Zeichen verbunden (Spec, Stufe 1, „Ausgang“). Thread-sicher:
    /// RICS sendet teils aus Hintergrund-Threads.
    /// </summary>
    public sealed class LineMerger
    {
        public const string Separator = " · ";

        public int MaxMessageLength { get; }
        public TimeSpan QuietPeriod { get; }
        public int MaxPendingLines { get; }

        private readonly object _lock = new object();
        private readonly LinkedList<string> _pending = new LinkedList<string>();
        private DateTime _lastAdded = DateTime.MinValue;
        private int _dropped;

        public LineMerger(int maxMessageLength = 500, TimeSpan? quietPeriod = null, int maxPendingLines = 50)
        {
            if (maxMessageLength < 20) throw new ArgumentOutOfRangeException(nameof(maxMessageLength));
            if (maxPendingLines < 1) throw new ArgumentOutOfRangeException(nameof(maxPendingLines));
            MaxMessageLength = maxMessageLength;
            QuietPeriod = quietPeriod ?? TimeSpan.FromSeconds(1);
            MaxPendingLines = maxPendingLines;
        }

        /// <summary>Anzahl verworfener Zeilen seit dem letzten Abruf (älteste fallen weg, wenn zu viele warten).</summary>
        public int TakeDroppedCount()
        {
            lock (_lock)
            {
                int d = _dropped;
                _dropped = 0;
                return d;
            }
        }

        public int PendingCount
        {
            get { lock (_lock) return _pending.Count; }
        }

        /// <summary>Nimmt eine Zeile auf. Leere Zeilen werden ignoriert, Zeilenumbrüche zu Leerzeichen.</summary>
        public void Add(string line, DateTime now)
        {
            string clean = Normalize(line);
            if (clean.Length == 0) return;
            lock (_lock)
            {
                foreach (string part in Split(clean, MaxMessageLength))
                {
                    _pending.AddLast(part);
                    if (_pending.Count > MaxPendingLines)
                    {
                        _pending.RemoveFirst();
                        _dropped++;
                    }
                }
                _lastAdded = now;
            }
        }

        /// <summary>
        /// Gibt fertige Nachrichten zurück, sobald seit der letzten Zeile <see cref="QuietPeriod"/> vergangen ist,
        /// oder sofort mit <paramref name="force"/>. Sonst eine leere Liste.
        /// </summary>
        public List<string> Flush(DateTime now, bool force = false)
        {
            var result = new List<string>();
            lock (_lock)
            {
                if (_pending.Count == 0) return result;
                if (!force && now - _lastAdded < QuietPeriod) return result;

                var current = new StringBuilder();
                foreach (string line in _pending)
                {
                    int needed = current.Length == 0 ? line.Length : current.Length + Separator.Length + line.Length;
                    if (needed > MaxMessageLength && current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    if (current.Length > 0) current.Append(Separator);
                    current.Append(line);
                }
                if (current.Length > 0) result.Add(current.ToString());
                _pending.Clear();
            }
            return result;
        }

        internal static string Normalize(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            var sb = new StringBuilder(line.Length);
            bool lastSpace = false;
            foreach (char c in line)
            {
                bool space = c == '\r' || c == '\n' || c == '\t' || c == ' ';
                if (space)
                {
                    if (!lastSpace && sb.Length > 0) sb.Append(' ');
                    lastSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastSpace = false;
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Teilt eine zu lange Zeile an Wortgrenzen, notfalls hart.</summary>
        internal static IEnumerable<string> Split(string line, int max)
        {
            while (line.Length > max)
            {
                int cut = line.LastIndexOf(' ', max);
                if (cut < max / 2) cut = max;
                yield return line.Substring(0, cut).TrimEnd();
                line = line.Substring(cut).TrimStart();
            }
            if (line.Length > 0) yield return line;
        }
    }
}
