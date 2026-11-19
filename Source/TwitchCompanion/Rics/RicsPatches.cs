// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Harmony-Patches an RICS (Spec, „Einstiegspunkte und Patches“).

using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace TwitchCompanion.RimWorld.Rics
{
    internal static class RicsPatches
    {
        public static void Apply(Harmony harmony)
        {
            // P1: Ausgang über die Mod statt über die eigene Twitch-Verbindung von RICS.
            harmony.Patch(RicsApi.SendMessage, prefix: new HarmonyMethod(typeof(RicsPatches), nameof(SendMessagePrefix)));
            harmony.Patch(RicsApi.SendWhisperAsync, prefix: new HarmonyMethod(typeof(RicsPatches), nameof(SendWhisperPrefix)));
            // P2: RICS antwortet auf Twitch nur, wenn es sich für verbunden hält.
            harmony.Patch(RicsApi.IsConnectedGetter, postfix: new HarmonyMethod(typeof(RicsPatches), nameof(IsConnectedPostfix)));
            // P3: Antworten auf Befehle direkt abgreifen, falls der kleine Getter aus P2 vom JIT eingebettet wurde.
            harmony.Patch(RicsApi.SendToPlatform, prefix: new HarmonyMethod(typeof(RicsPatches), nameof(SendToPlatformPrefix)));
            Log.Message("[Twitch Companion] Patches an RICS gesetzt (P1, P2, P3).");
        }

        // Kann aus Hintergrund-Threads kommen; nur in die thread-sichere Ausgabe legen.
        private static bool SendMessagePrefix(string message)
        {
            if (!Bridge.IsActive) return true;
            Bridge.EnqueueOutgoing(message);
            return false;
        }

        // Flüstern wird nicht unterstützt: als öffentliche Antwort, wie es RICS im Fehlerfall auch tut.
        private static bool SendWhisperPrefix(string username, string message, ref Task __result)
        {
            if (!Bridge.IsActive) return true;
            Bridge.EnqueueOutgoing("@" + username + " " + message);
            __result = Task.CompletedTask;
            return false;
        }

        private static bool SendToPlatformPrefix(string platform, string username, string cleanText, bool alreadyAddressed)
        {
            if (!Bridge.IsActive || !string.Equals(platform, "twitch", System.StringComparison.OrdinalIgnoreCase)) return true;
            Bridge.EnqueueOutgoing(alreadyAddressed ? cleanText : "@" + username + " " + cleanText);
            return false;
        }

        private static void IsConnectedPostfix(ref bool __result)
        {
            if (Bridge.IsActive) __result = true;
        }
    }
}
