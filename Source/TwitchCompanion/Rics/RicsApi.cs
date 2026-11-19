// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Zugriff auf RICS zur Laufzeit.

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TwitchCompanion.RimWorld.Core;
using Verse;

namespace TwitchCompanion.RimWorld.Rics
{
    /// <summary>
    /// Findet die genutzten RICS-Stellen per Reflexion (keine Kompilier-Abhängigkeit von RICS). Fehlt eine Stelle
    /// nach einem RICS-Update, bleibt die Brücke aus und <see cref="Problem"/> nennt den Grund.
    /// Einstiegspunkte: Spec, „Einstiegspunkte und Patches“ (geprüft an RICS-Branch 1.6, Version 1.57).
    /// </summary>
    internal static class RicsApi
    {
        private const string Ns = "CAP_ChatInteractive.";

        public static bool Available { get; private set; }
        public static string Problem { get; private set; } = "noch nicht geprüft";

        public static MethodInfo SendMessage { get; private set; }
        public static MethodInfo SendWhisperAsync { get; private set; }
        public static MethodInfo IsConnectedGetter { get; private set; }
        public static MethodInfo SendToPlatform { get; private set; }

        private static ConstructorInfo _wrapperCtor;
        private static ParameterInfo[] _wrapperParams;
        private static MethodInfo _processMessage;
        private static MethodInfo _updateActivity;
        private static MethodInfo _getViewer;

        public static void Resolve()
        {
            try
            {
                Type processor = AccessTools.TypeByName(Ns + "ChatCommandProcessor");
                if (processor == null) { Fail("RICS nicht geladen (ChatCommandProcessor fehlt)"); return; }

                Assembly rics = processor.Assembly;
                Type wrapper = rics.GetType("ChatMessageWrapper") ?? rics.GetTypes().FirstOrDefault(t => t.Name == "ChatMessageWrapper");
                Type viewers = rics.GetType(Ns + "Viewers");
                Type twitch = rics.GetType(Ns + "TwitchService");
                if (wrapper == null || viewers == null || twitch == null) { Fail("RICS-Typen fehlen (ChatMessageWrapper, Viewers oder TwitchService)"); return; }

                _wrapperCtor = wrapper.GetConstructors().FirstOrDefault(c =>
                {
                    var p = c.GetParameters();
                    return p.Length >= 4 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(string)
                        && p[2].ParameterType == typeof(string) && p.Any(x => x.Name == "platformUserId");
                });
                _wrapperParams = _wrapperCtor?.GetParameters();
                _processMessage = AccessTools.Method(processor, "ProcessMessage", new[] { wrapper });
                _updateActivity = AccessTools.Method(viewers, "UpdateViewerActivity", new[] { wrapper });
                _getViewer = AccessTools.Method(viewers, "GetViewer", new[] { wrapper });
                SendMessage = AccessTools.Method(twitch, "SendMessage", new[] { typeof(string) });
                SendWhisperAsync = AccessTools.Method(twitch, "SendWhisperAsync", new[] { typeof(string), typeof(string) });
                IsConnectedGetter = AccessTools.PropertyGetter(twitch, "IsConnected");
                SendToPlatform = AccessTools.Method(processor, "SendToPlatform");

                string missing = string.Join(", ", new (string name, object found)[]
                {
                    ("ChatMessageWrapper-Konstruktor", _wrapperCtor),
                    ("ChatCommandProcessor.ProcessMessage", _processMessage),
                    ("Viewers.UpdateViewerActivity", _updateActivity),
                    ("Viewers.GetViewer", _getViewer),
                    ("TwitchService.SendMessage", SendMessage),
                    ("TwitchService.SendWhisperAsync", SendWhisperAsync),
                    ("TwitchService.IsConnected", IsConnectedGetter),
                    ("ChatCommandProcessor.SendToPlatform", SendToPlatform),
                }.Where(x => x.found == null).Select(x => x.name));

                if (missing.Length > 0) { Fail("RICS-Stellen fehlen: " + missing); return; }

                Available = true;
                Problem = null;
            }
            catch (Exception ex)
            {
                Fail("Fehler beim Suchen der RICS-Stellen: " + ex.Message);
            }
        }

        private static void Fail(string reason)
        {
            Available = false;
            Problem = reason;
            Log.Warning("[Twitch Companion] Brücke aus: " + reason);
        }

        /// <summary>Gibt eine Chatnachricht an RICS weiter, wie es RICS nach dem Empfang selbst tut. Nur im Hauptthread.</summary>
        public static void Deliver(ChatLine line)
        {
            if (!Available) return;

            var args = new object[_wrapperParams.Length];
            for (int i = 0; i < _wrapperParams.Length; i++)
                args[i] = _wrapperParams[i].HasDefaultValue ? _wrapperParams[i].DefaultValue : null;
            args[0] = line.NameForRics;
            args[1] = line.Text;
            args[2] = "twitch";
            args[Array.FindIndex(_wrapperParams, p => p.Name == "platformUserId")] = line.UserId;

            object wrapper = _wrapperCtor.Invoke(args);

            _updateActivity.Invoke(null, new[] { wrapper });
            object viewer = _getViewer.Invoke(null, new[] { wrapper });
            if (viewer != null)
            {
                // RICS liest Rollen sonst aus der TwitchLib-Nachricht, die es hier nicht gibt.
                SetBool(viewer, "IsBroadcaster", line.IsBroadcaster);
                SetBool(viewer, "IsModerator", line.IsModerator);
                SetBool(viewer, "IsVip", line.IsVip);
                SetBool(viewer, "IsSubscriber", line.IsSubscriber);
            }
            _processMessage.Invoke(null, new[] { wrapper });
        }

        private static void SetBool(object target, string property, bool value)
        {
            PropertyInfo p = AccessTools.Property(target.GetType(), property);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool)) p.SetValue(target, value);
        }
    }
}
