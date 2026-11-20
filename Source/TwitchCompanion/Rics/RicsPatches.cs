// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Harmony-Patches an RICS (Spec, „Einstiegspunkte und Patches“).

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using TwitchCompanion.RimWorld.Core;
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
            // P4: Coins nur in der Kategorie RimWorld (RICS selbst prüft weder Verbindung noch Kategorie).
            if (RicsApi.CoinReward != null)
                harmony.Patch(RicsApi.CoinReward, prefix: new HarmonyMethod(typeof(RicsPatches), nameof(CoinRewardPrefix)));
            int lootbox = PatchLootbox(harmony);
            // P6: kurze deutsche Antwort auf !bal (Nutzer, 20.11.2026).
            MethodInfo bal = AccessTools.Method(AccessTools.TypeByName("CAP_ChatInteractive.Commands.ViewerCommands.Bal"), "Execute");
            if (bal != null) harmony.Patch(bal, postfix: new HarmonyMethod(typeof(RicsPatches), nameof(BalPostfix)));
            Log.Message("[Twitch Companion] Patches an RICS gesetzt (P1, P2, P3" + (RicsApi.CoinReward != null ? ", P4" : "; P4 fehlt")
                + (lootbox > 0 ? ", P5 an " + lootbox + " Stellen" : "; P5 fehlt") + (bal != null ? ", P6)." : "; P6 fehlt)."));
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

        // ── P5: Lootbox-Pfennig nach eigenen Chancen (Feature-Issue #10) ──

        /// <summary>
        /// Ersetzt in <c>LootBoxCommandHandler</c> jeden Aufruf <c>Rand.Range(min, max)</c>, dessen zweites Argument
        /// <c>LootBoxRandomCoinRange.max</c> ist, durch <see cref="RollLootbox"/>. Gibt die Zahl der Methoden zurück.
        /// </summary>
        private static int PatchLootbox(Harmony harmony)
        {
            System.Type handler = AccessTools.TypeByName("CAP_ChatInteractive.Commands.ViewerCommands.LootBoxCommandHandler");
            if (handler == null) return 0;
            int patched = 0;
            foreach (MethodInfo m in AccessTools.GetDeclaredMethods(handler))
            {
                try
                {
                    List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(m);
                    if (!Enumerable.Range(0, original.Count).Any(i => IsLootboxRoll(original, i))) continue;
                    harmony.Patch(m, transpiler: new HarmonyMethod(typeof(RicsPatches), nameof(LootboxTranspiler)));
                    patched++;
                }
                catch (System.Exception ex) { Log.Warning("[Twitch Companion] P5 an " + m.Name + " nicht möglich: " + ex.Message); }
            }
            return patched;
        }

        private static readonly MethodInfo RandRange = AccessTools.Method(typeof(Rand), nameof(Rand.Range), new[] { typeof(int), typeof(int) });
        private static readonly FieldInfo RangeMax = AccessTools.Field(typeof(IntRange), nameof(IntRange.max));

        private static bool IsLootboxRoll(List<CodeInstruction> code, int i) =>
            i > 0 && code[i].Calls(RandRange) && code[i - 1].LoadsField(RangeMax);

        private static IEnumerable<CodeInstruction> LootboxTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            MethodInfo roll = AccessTools.Method(typeof(RicsPatches), nameof(RollLootbox));
            for (int i = 0; i < code.Count; i++)
                if (IsLootboxRoll(code, i))
                    code[i] = new CodeInstruction(OpCodes.Call, roll) { labels = code[i].labels, blocks = code[i].blocks };
            return code;
        }

        /// <summary>Gleiche Signatur wie <c>Rand.Range(int, int)</c>; nutzt nur den Höchstwert aus RICS.</summary>
        public static int RollLootbox(int min, int max)
        {
            if (TwitchCompanionMod.Settings == null || !TwitchCompanionMod.Settings.lootboxOdds) return Rand.Range(min, max);
            return LootboxOdds.Roll(max, () => Rand.Value);
        }

        // ── P6: !bal kurz und deutsch ──
        private static void BalPostfix(object messageWrapper, ref string __result)
        {
            if (TwitchCompanionMod.Settings == null || !TwitchCompanionMod.Settings.germanChat || messageWrapper == null) return;
            try
            {
                string shortText = RicsEconomy.ShortBalance(messageWrapper);
                if (shortText != null) __result = shortText;
            }
            catch (System.Exception ex) { Log.Warning("[Twitch Companion] Kurze !bal-Antwort nicht möglich: " + ex.Message); }
        }

        // Läuft im Spieltakt. Überspringt die Auszahlung, solange die Brücke Coins sperrt; RICS zahlt danach einmal nach.
        private static bool CoinRewardPrefix() => !Bridge.BlocksCoins;
    }
}
