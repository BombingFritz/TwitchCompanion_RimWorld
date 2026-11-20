// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Hinweis an Zuschauer mit !privat, höchstens einmal je Stream.

using System;
using System.Collections.Generic;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>
    /// Merkt sich (nur im Arbeitsspeicher), wem der Hinweis schon gegeben wurde. Keine Speicherung über den
    /// Spielstart hinaus, passend zu <c>!privat</c> (Spec, Abschnitt „!privat“). <see cref="Reset"/> beim Streamstart.
    /// </summary>
    public sealed class PrivacyNotices
    {
        public const string Text = "Mit !privat spielst du in RimWorld nicht mit.";

        private readonly HashSet<string> _notified = new HashSet<string>(StringComparer.Ordinal);
        private readonly object _lock = new object();

        /// <summary>Wahr, wenn dieser Zuschauer den Hinweis jetzt bekommen soll (erstes Mal).</summary>
        public bool ShouldNotify(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return false;
            lock (_lock) return _notified.Add(userId);
        }

        public void Reset()
        {
            lock (_lock) _notified.Clear();
        }
    }
}
