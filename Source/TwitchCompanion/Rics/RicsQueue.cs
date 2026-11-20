// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Warteschlange zum Übernehmen (Nutzer, 20.11.2026): Neue vorne, einmal je Stream,
// Leeren nach dem Stream, Zuweisen ohne !acceptpawn.

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TwitchCompanion.RimWorld.Core;
using Verse;

namespace TwitchCompanion.RimWorld.Rics
{
    internal static class RicsQueue
    {
        private const string Ns = "CAP_ChatInteractive.";
        private static readonly Type ManagerType = AccessTools.TypeByName(Ns + "GameComponent_PawnAssignmentManager");
        private static readonly FieldInfo QueueField = ManagerType == null ? null : AccessTools.Field(ManagerType, "pawnQueue");
        private static readonly Type DialogType = AccessTools.TypeByName(Ns + "Dialog_PawnQueue");
        private static readonly FieldInfo SelectedUsername = DialogType == null ? null : AccessTools.Field(DialogType, "selectedUsername");
        private static readonly FieldInfo SelectedPlatformId = DialogType == null ? null : AccessTools.Field(DialogType, "selectedUserPlatformID");

        private static object Manager =>
            ManagerType == null || Current.Game == null ? null : AccessTools.Method(typeof(Game), "GetComponent", Type.EmptyTypes)?.MakeGenericMethod(ManagerType).Invoke(Current.Game, null);

        private static List<string> Queue(object manager) => QueueField?.GetValue(manager) as List<string>;

        /// <summary>P8 und P9 setzen; liefert die gesetzten Kürzel für das Log.</summary>
        public static string Apply(Harmony harmony)
        {
            var done = new List<string>();
            MethodInfo add = ManagerType == null ? null : AccessTools.Method(ManagerType, "AddToQueue");
            if (add != null && QueueField != null)
            {
                harmony.Patch(add, postfix: new HarmonyMethod(typeof(RicsQueue), nameof(AddToQueuePostfix)));
                done.Add("P8");
            }
            MethodInfo draw = DialogType == null ? null : AccessTools.Method(DialogType, "DoWindowContents");
            if (draw != null && SelectedUsername != null && SelectedPlatformId != null && QueueField != null)
            {
                harmony.Patch(draw, prefix: new HarmonyMethod(typeof(RicsQueue), nameof(DialogPrefix)));
                done.Add("P9");
            }
            return done.Count == 0 ? "P8 und P9 fehlen" : string.Join(", ", done);
        }

        /// <summary>P8: Wer sich zuletzt anstellt, steht vorne (RICS hängt hinten an).</summary>
        private static void AddToQueuePostfix(object __instance, bool __result)
        {
            if (!__result) return;
            try { QueueRules.MoveLastToFront(Queue(__instance)); }
            catch (Exception ex) { Log.Warning("[Twitch Companion] Warteschlange nicht umsortiert: " + ex.Message); }
        }

        private static object _dialog;
        private static string _preselected;

        /// <summary>
        /// P9: Im Fenster „Pawn Queue“ ist immer der Vorderste vorausgewählt; „Assign“ vergibt dann sofort, ohne
        /// <c>!acceptpawn</c>. Neu gewählt wird nur, wenn sich der Vorderste ändert, damit „Select“ und Eintippen weiter gehen.
        /// </summary>
        private static void DialogPrefix(object __instance)
        {
            try
            {
                if (!ReferenceEquals(__instance, _dialog)) { _dialog = __instance; _preselected = null; }
                object manager = Manager;
                List<string> queue = manager == null ? null : Queue(manager);
                string front = queue != null && queue.Count > 0 ? queue[0] : null;
                if (front == null || front == _preselected) return;
                _preselected = front;
                string name = AccessTools.Method(ManagerType, "GetUsernameFromPlatformId")?.Invoke(manager, new object[] { front }) as string;
                if (string.IsNullOrEmpty(name)) return;
                SelectedUsername.SetValue(__instance, name);
                SelectedPlatformId.SetValue(__instance, front);
            }
            catch (Exception ex) { Log.WarningOnce("[Twitch Companion] Vorauswahl in der Warteschlange fehlgeschlagen: " + ex.Message, 0x7C0A9); }
        }

        public static bool IsInQueue(string username)
        {
            object manager = Manager;
            MethodInfo m = ManagerType == null ? null : AccessTools.Method(ManagerType, "IsInQueue", new[] { typeof(string) });
            return manager != null && m != null && (bool)m.Invoke(manager, new object[] { username });
        }

        /// <summary>Leert die Warteschlange des geladenen Spielstands; falsch ohne Spielstand oder RICS-Stelle.</summary>
        public static bool Clear()
        {
            object manager = Manager;
            MethodInfo m = ManagerType == null ? null : AccessTools.Method(ManagerType, "ClearQueue");
            if (manager == null || m == null) return false;
            m.Invoke(manager, null);
            return true;
        }
    }
}
