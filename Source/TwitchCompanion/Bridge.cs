// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Ein- und Ausgang zwischen Server und RICS.

using System;
using System.Collections.Concurrent;
using TwitchCompanion.RimWorld.Core;
using TwitchCompanion.RimWorld.Rics;
using Verse;

namespace TwitchCompanion.RimWorld
{
    /// <summary>
    /// Verbindet Eingang (Befehle) und Ausgang (Chatzeilen). Die Serveranbindung fehlt noch, bis die Schnittstelle
    /// im Server vereinbart ist (Server-Issues #74, #79). Bis dahin gibt es nur den Testmodus:
    /// Nachrichten werden in den Einstellungen eingespeist, Antworten landen im Log.
    /// </summary>
    internal static class Bridge
    {
        private const int MaxIncoming = 200;
        private const int MaxDeliveriesPerFrame = 20;
        private const string LogPrefix = "[Twitch Companion] ";

        public static readonly LineMerger Outgoing = new LineMerger();
        public static readonly PrivacyNotices Privacy = new PrivacyNotices();
        private static readonly ConcurrentQueue<ChatLine> Incoming = new ConcurrentQueue<ChatLine>();

        public static string LastError { get; private set; }

        private static CompanionSettings Settings => TwitchCompanionMod.Settings;

        /// <summary>Verbindung zum Server steht. Heute nur im Testmodus.</summary>
        public static bool LinkConnected => Settings != null && Settings.testMode;

        /// <summary>Brücke an, RICS gefunden und verbunden: Dann gehen RICS-Antworten über die Mod.</summary>
        public static bool IsActive => Settings != null && Settings.enabled && RicsApi.Available && LinkConnected;

        public static void EnqueueIncoming(ChatLine line)
        {
            if (line == null) return;
            Incoming.Enqueue(line);
            while (Incoming.Count > MaxIncoming && Incoming.TryDequeue(out _)) { }
        }

        public static void EnqueueOutgoing(string text) => Outgoing.Add(text, DateTime.UtcNow);

        /// <summary>Im Hauptthread aufrufen (GameComponent). Liefert Eingänge an RICS und gibt Ausgänge weiter.</summary>
        public static void MainThreadTick()
        {
            for (int i = 0; i < MaxDeliveriesPerFrame && Incoming.TryDequeue(out ChatLine line); i++)
                Deliver(line);

            foreach (string message in Outgoing.Flush(DateTime.UtcNow))
                Send(message);

            int dropped = Outgoing.TakeDroppedCount();
            if (dropped > 0) Log.Warning(LogPrefix + dropped + " Chatzeile(n) verworfen, zu viele warteten.");
        }

        private static void Deliver(ChatLine line)
        {
            if (!IsActive || !line.IsComplete) return;

            if (line.IsPrivate)
            {
                if (Privacy.ShouldNotify(line.UserId))
                    EnqueueOutgoing("@" + line.Login + " " + PrivacyNotices.Text);
                return;
            }

            try
            {
                RicsApi.Deliver(line);
            }
            catch (Exception ex)
            {
                LastError = (ex.InnerException ?? ex).Message;
                Log.Error(LogPrefix + "Nachricht konnte nicht an RICS gehen: " + (ex.InnerException ?? ex));
            }
        }

        private static void Send(string message)
        {
            if (Settings.testMode)
            {
                Log.Message(LogPrefix + "Chat ← " + message);
                return;
            }
            // Serveranbindung folgt nach Vereinbarung der Schnittstelle (Server-Issue #74).
        }
    }
}
