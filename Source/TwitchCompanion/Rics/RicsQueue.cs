// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Warteschlange zum Übernehmen (Nutzer, 20.11.2026): Neue vorne, einmal je Stream,
// Leeren nach dem Stream, Zuweisen ohne !acceptpawn.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TwitchCompanion.RimWorld.Core;
using RimWorld;
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
            // P10: !leave – Kolonist bleibt in der Kolonie (RICS schickt ihn sonst weg), kein Abschiedsbrief, eigene Antwort.
            Type leave = AccessTools.TypeByName(Ns + "Commands.ViewerCommands.Leave");
            MethodInfo depart = leave == null ? null : AccessTools.Method(leave, "PreparePawnForDeparture");
            MethodInfo letter = leave == null ? null : AccessTools.Method(leave, "SendDepartureLetter");
            MethodInfo execute = leave == null ? null : AccessTools.Method(leave, "Execute");
            if (depart != null && letter != null && execute != null)
            {
                harmony.Patch(depart, prefix: new HarmonyMethod(typeof(RicsQueue), nameof(Skip)));
                harmony.Patch(letter, prefix: new HarmonyMethod(typeof(RicsQueue), nameof(LetterPrefix)));
                harmony.Patch(execute, prefix: new HarmonyMethod(typeof(RicsQueue), nameof(LeavePrefix)),
                    postfix: new HarmonyMethod(typeof(RicsQueue), nameof(LeavePostfix)));
                done.Add("P10");
            }
            // P11: Wer seinen Kolonisten verliert, macht ihn zum „Dulli“.
            MethodInfo unassign = ManagerType == null ? null : AccessTools.Method(ManagerType, "UnassignCore");
            if (unassign != null && AssignmentsField != null)
            {
                harmony.Patch(unassign, prefix: new HarmonyMethod(typeof(RicsQueue), nameof(UnassignPrefix)),
                    postfix: new HarmonyMethod(typeof(RicsQueue), nameof(UnassignPostfix)));
                done.Add("P11");
            }
            // P12: Im Fenster „Pawn Queue“ nicht an jemanden vergeben, der schon einen lebenden Kolonisten hat.
            MethodInfo assignDirect = DialogType == null ? null : AccessTools.Method(DialogType, "AssignPawnDirectly");
            if (assignDirect != null)
            {
                harmony.Patch(assignDirect, prefix: new HarmonyMethod(typeof(RicsQueue), nameof(AssignDirectlyPrefix)));
                done.Add("P12");
            }
            // P14: RICS ruft beim Zuweisen und Entfernen RemoveFromQueue(string) mit der Plattform-ID („twitch:123“) statt mit dem
            // Namen auf; Viewers.GetViewer legt dafür einen Scheinzuschauer mit Startguthaben an, den RICS später dem echten
            // Zuschauer gutschreibt (Stream vom 20.11.2026: je Zuweisung +100 Pfennig und +100 Karma).
            MethodInfo removeByName = ManagerType == null ? null : AccessTools.Method(ManagerType, "RemoveFromQueue", new[] { typeof(string) });
            if (removeByName != null && QueueField != null)
            {
                harmony.Patch(removeByName, prefix: new HarmonyMethod(typeof(RicsQueue), nameof(RemoveByIdPrefix)));
                done.Add("P14");
            }
            return done.Count == 0 ? "P8 bis P14 fehlen" : string.Join(", ", done);
        }

        private static readonly FieldInfo AssignmentsField = ManagerType == null ? null : AccessTools.Field(ManagerType, "viewerPawnAssignments");
        private static readonly MethodInfo FindPawn = ManagerType == null ? null : AccessTools.Method(ManagerType, "FindPawnByThingId");

        private static bool Skip() => false;

        private static readonly FieldInfo JoinTimesField = ManagerType == null ? null : AccessTools.Field(ManagerType, "queueJoinTimes");

        /// <summary>P14: Plattform-ID direkt aus der Warteschlange nehmen, ohne einen Zuschauer anzulegen.</summary>
        private static bool RemoveByIdPrefix(object __instance, string username, ref bool __result)
        {
            if (!QueueRules.IsPlatformId(username)) return true;
            try
            {
                List<string> queue = Queue(__instance);
                __result = queue != null && queue.Remove(username);
                if (__result && JoinTimesField?.GetValue(__instance) is System.Collections.IDictionary times) times.Remove(username);
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning("[Twitch Companion] P14: " + ex.Message);
                return true;
            }
        }

        private static Pawn PawnOf(object manager, string identifier)
        {
            if (manager == null || string.IsNullOrEmpty(identifier) || !(AssignmentsField?.GetValue(manager) is Dictionary<string, string> map)) return null;
            return map.TryGetValue(identifier, out string thingId) ? FindPawn?.Invoke(null, new object[] { thingId }) as Pawn : null;
        }

        private static bool DulliMode => TwitchCompanionMod.Settings?.leaveMode != LeaveMode.Departs;

        /// <summary>Abschiedsbrief von RICS nur, wenn der Kolonist wirklich geht.</summary>
        private static bool LetterPrefix() => !DulliMode;

        private static void UnassignPrefix(object __instance, string identifier, out Pawn __state)
        {
            __state = PawnOf(__instance, identifier);
            _nameBefore = __state?.Name;
        }

        private static Name _nameBefore;
        private static bool _inLeave;
        private static Pawn _departing;

        /// <summary>
        /// Nach dem Lösen setzt RICS den alten Spitznamen. Bleibt der Kolonist, setzt die Mod „Dulli“, „Dulli 2“, …;
        /// geht er per <c>!leave</c>, behält er den Namen des Zuschauers und wird nach dem Befehl verbannt.
        /// </summary>
        private static void UnassignPostfix(bool __result, Pawn __state)
        {
            if (!__result || __state == null || __state.Dead) return;
            if (!DulliMode)
            {
                if (_inLeave) { if (_nameBefore != null) __state.Name = _nameBefore; _departing = __state; }
                return;
            }
            try
            {
                var used = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Where(p => p != __state && p.Name != null)
                    .Select(p => p.Name is NameTriple t ? t.Nick : p.Name.ToStringShort);
                string nick = DulliNames.Next(used);
                __state.Name = __state.Name is NameTriple n ? new NameTriple(n.First, nick, n.Last) : (Name)new NameSingle(nick);
                LastDulli = nick;
            }
            catch (Exception ex) { Log.Warning("[Twitch Companion] Dulli-Name nicht gesetzt: " + ex.Message); }
        }

        /// <summary>Zuletzt vergebener Dulli-Name (Hauptthread).</summary>
        public static string LastDulli;

        private static void LeavePrefix()
        {
            LastDulli = null;
            _departing = null;
            _inLeave = true;
        }

        private static void LeavePostfix(ref string __result)
        {
            _inLeave = false;
            if (LastDulli != null) { __result = "✅ Freigegeben. Dein Kolonist bleibt als „" + LastDulli + "“ in der Kolonie."; return; }
            Pawn pawn = _departing;
            _departing = null;
            if (pawn == null || pawn.Dead) return;
            try
            {
                // Wie „verbannen“ im Spiel: verlässt die Kolonie und zieht in die Spielwelt.
                if (pawn.Faction == Faction.OfPlayer) PawnBanishUtility.Banish(pawn, giveThoughts: false);
                __result = "👋 " + pawn.Name.ToStringShort + " verlässt die Kolonie und zieht in die Welt hinaus.";
            }
            catch (Exception ex) { Log.Warning("[Twitch Companion] Kolonist nicht verabschiedet: " + ex.Message); }
        }

        private static bool AssignDirectlyPrefix(string username)
        {
            try
            {
                object manager = Manager;
                MethodInfo has = ManagerType == null ? null : AccessTools.Method(ManagerType, "HasAssignedPawn", new[] { typeof(string) });
                if (manager == null || has == null || string.IsNullOrEmpty(username) || !(bool)has.Invoke(manager, new object[] { username })) return true;
                Remove(username);
                Messages.Message(username + " hat schon einen Kolonisten und ist aus der Warteschlange raus.", MessageTypeDefOf.RejectInput, false);
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning("[Twitch Companion] Prüfung vor dem Zuweisen fehlgeschlagen: " + ex.Message);
                return true;
            }
        }

        /// <summary>Nimmt jemanden aus der Warteschlange (z. B. nach einem gekauften Kolonisten).</summary>
        public static void Remove(string username)
        {
            object manager = Manager;
            MethodInfo m = ManagerType == null ? null : AccessTools.Method(ManagerType, "RemoveFromQueue", new[] { typeof(string) });
            if (manager != null && m != null && !string.IsNullOrEmpty(username)) m.Invoke(manager, new object[] { username });
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
