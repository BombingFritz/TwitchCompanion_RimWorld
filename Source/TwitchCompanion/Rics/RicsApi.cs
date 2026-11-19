// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Zugriff auf RICS zur Laufzeit.

using System;
using System.Collections.Generic;
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
    /// Einstiegspunkte: Spec, „Einstiegspunkte und Patches“ (geprüft an RICS 1.56, Tag <c>RICS_1.56</c>, und 1.57).
    /// </summary>
    internal static class RicsApi
    {
        private const string Ns = "CAP_ChatInteractive.";
        /// <summary>Plattformname wie im eigenen Twitch-Empfang von RICS (<c>TwitchService</c>).</summary>
        public const string Platform = "Twitch";

        public static bool Available { get; private set; }
        public static string Problem { get; private set; } = "noch nicht geprüft";

        public static MethodInfo SendMessage { get; private set; }
        public static MethodInfo SendWhisperAsync { get; private set; }
        public static MethodInfo IsConnectedGetter { get; private set; }
        public static MethodInfo SendToPlatform { get; private set; }
        /// <summary>Coin-Vergabe von RICS alle 2 min (P4); fehlt sie, gibt es nur eine Warnung.</summary>
        public static MethodInfo CoinReward { get; private set; }

        private static ConstructorInfo _wrapperCtor;
        private static ParameterInfo[] _wrapperParams;
        private static MethodInfo _processMessage;
        private static MethodInfo _updateActivity;
        private static MethodInfo _getViewer;
        private static MethodInfo _getViewerByPlatformId;
        private static MethodInfo _chatLogAdd;
        private static PropertyInfo _modInstance;
        private static PropertyInfo _modSettings;
        private static FieldInfo _globalSettings;
        private static PropertyInfo _lastSeen;

        public static void Resolve()
        {
            try
            {
                Type processor = AccessTools.TypeByName(Ns + "ChatCommandProcessor");
                if (processor == null) { Fail("RICS nicht geladen (ChatCommandProcessor fehlt)"); return; }

                Assembly rics = processor.Assembly;
                Type wrapper = rics.GetType("ChatMessageWrapper") ?? rics.GetTypes().FirstOrDefault(t => t.Name == "ChatMessageWrapper");
                Type viewers = rics.GetType(Ns + "Viewers");
                Type viewer = rics.GetType(Ns + "Viewer");
                Type twitch = rics.GetType(Ns + "TwitchService");
                Type mod = rics.GetType(Ns + "CAPChatInteractiveMod");
                if (wrapper == null || viewers == null || viewer == null || twitch == null || mod == null)
                { Fail("RICS-Typen fehlen (ChatMessageWrapper, Viewers, Viewer, TwitchService oder CAPChatInteractiveMod)"); return; }

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
                _getViewerByPlatformId = AccessTools.Method(viewers, "GetViewerByPlatformId", new[] { typeof(string), typeof(string) });
                _lastSeen = AccessTools.Property(viewer, "LastSeen");
                SendMessage = AccessTools.Method(twitch, "SendMessage", new[] { typeof(string) });
                SendWhisperAsync = AccessTools.Method(twitch, "SendWhisperAsync", new[] { typeof(string), typeof(string) });
                IsConnectedGetter = AccessTools.PropertyGetter(twitch, "IsConnected");
                SendToPlatform = AccessTools.Method(processor, "SendToPlatform");
                _modInstance = AccessTools.Property(mod, "Instance");
                _modSettings = AccessTools.Property(mod, "Settings");
                _globalSettings = _modSettings == null ? null : AccessTools.Field(_modSettings.PropertyType, "GlobalSettings");

                // Nicht zwingend: Anzeige im RICS-Fenster „Live Chat“ und Coin-Sperre (P4).
                Type chatLog = rics.GetType(Ns + "ChatMessageLogger") ?? rics.GetTypes().FirstOrDefault(t => t.Name == "ChatMessageLogger");
                _chatLogAdd = chatLog == null ? null : AccessTools.Method(chatLog, "AddMessage", new[] { typeof(string), typeof(string), typeof(string), typeof(bool) });
                CoinReward = AccessTools.Method(rics.GetType(Ns + "CAPChatInteractive_GameComponent"), "TryAwardWallClockCoinReward");

                string missing = string.Join(", ", new (string name, object found)[]
                {
                    ("ChatMessageWrapper-Konstruktor", _wrapperCtor),
                    ("ChatCommandProcessor.ProcessMessage", _processMessage),
                    ("Viewers.UpdateViewerActivity", _updateActivity),
                    ("Viewers.GetViewer", _getViewer),
                    ("Viewers.GetViewerByPlatformId", _getViewerByPlatformId),
                    ("Viewer.LastSeen", _lastSeen != null && _lastSeen.CanWrite ? _lastSeen : null),
                    ("TwitchService.SendMessage", SendMessage),
                    ("TwitchService.SendWhisperAsync", SendWhisperAsync),
                    ("TwitchService.IsConnected", IsConnectedGetter),
                    ("ChatCommandProcessor.SendToPlatform", SendToPlatform),
                    ("CAPChatInteractiveMod.Instance/Settings.GlobalSettings", _globalSettings != null && _modInstance != null ? _globalSettings : null),
                }.Where(x => x.found == null).Select(x => x.name));

                if (missing.Length > 0) { Fail("RICS-Stellen fehlen: " + missing); return; }

                if (_chatLogAdd == null) Log.Warning("[Twitch Companion] ChatMessageLogger.AddMessage nicht gefunden; Nachrichten erscheinen nicht im RICS-Fenster „Live Chat“.");
                if (CoinReward == null) Log.Warning("[Twitch Companion] Coin-Vergabe von RICS nicht gefunden (P4); RICS verteilt dann auch außerhalb der Kategorie RimWorld Coins.");

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

        /// <summary>
        /// Präfixe, die die Mod beim Server anmeldet: RICS-Einstellungen <c>Prefix</c> und <c>BuyPrefix</c>, dazu immer
        /// <c>$</c> (RICS wertet <c>$…</c> stets als Kauf). Nur 1–3 Zeichen ohne Komma (Server-Spec).
        /// </summary>
        public static string[] GetPrefixes()
        {
            var result = new List<string>();
            try
            {
                object global = GetGlobalSettings();
                Add(result, global == null ? null : AccessTools.Field(global.GetType(), "Prefix")?.GetValue(global) as string);
                Add(result, global == null ? null : AccessTools.Field(global.GetType(), "BuyPrefix")?.GetValue(global) as string);
            }
            catch (Exception) { /* Vorgaben unten */ }
            Add(result, "!");
            Add(result, "$");
            return result.ToArray();

            void Add(List<string> list, string p)
            {
                p = p?.Trim();
                if (string.IsNullOrEmpty(p) || p.Length > 3 || p.Contains(",") || list.Contains(p)) return;
                list.Add(p);
            }
        }

        private static object GetGlobalSettings()
        {
            object mod = _modInstance?.GetValue(null, null);
            object settings = mod == null ? null : _modSettings.GetValue(mod, null);
            return settings == null ? null : _globalSettings.GetValue(settings);
        }

        private static object NewWrapper(string username, string text, string userId)
        {
            var args = new object[_wrapperParams.Length];
            for (int i = 0; i < _wrapperParams.Length; i++)
                args[i] = _wrapperParams[i].HasDefaultValue ? _wrapperParams[i].DefaultValue : null;
            args[0] = username;
            args[1] = text;
            args[2] = Platform;
            args[Array.FindIndex(_wrapperParams, p => p.Name == "platformUserId")] = userId;
            return _wrapperCtor.Invoke(args);
        }

        /// <summary>
        /// Gibt eine Chatnachricht an RICS weiter wie der eigene Twitch-Empfang von RICS
        /// (<c>TwitchService.ProcessMessageOnMainThread</c>): Aktivität, Anzeige im Live-Chat, Befehl. Nur im Hauptthread.
        /// </summary>
        public static void Deliver(ChatLine line)
        {
            if (!Available) return;

            object wrapper = NewWrapper(line.NameForRics, line.Text, line.UserId);

            _updateActivity.Invoke(null, new[] { wrapper });
            object viewer = _getViewer.Invoke(null, new[] { wrapper });
            if (viewer != null)
                SetRoles(viewer, line.IsBroadcaster, line.IsModerator, line.IsVip, line.IsSubscriber);

            try { _chatLogAdd?.Invoke(null, new object[] { line.Login, line.Text, Platform, false }); }
            catch (Exception) { /* nur Anzeige */ }

            _processMessage.Invoke(null, new[] { wrapper });
        }

        /// <summary>
        /// Frischt die Aktivität plaudernder Zuschauer auf, damit RICS ihnen Coins gibt (<c>MinutesForActive</c>).
        /// Setzt nur <c>LastSeen</c> und die Rollen, nicht die Nachrichtenzählung; unbekannte Zuschauer legt RICS an.
        /// Nur im Hauptthread. Liefert (aufgefrischt, neu angelegt).
        /// </summary>
        public static (int refreshed, int created) RefreshActivity(List<ActiveChatter> chatters)
        {
            if (!Available || chatters == null) return (0, 0);
            int refreshed = 0, created = 0;
            DateTime now = DateTime.Now;
            foreach (ActiveChatter c in chatters)
            {
                try
                {
                    object viewer = _getViewerByPlatformId.Invoke(null, new object[] { Platform.ToLowerInvariant(), c.UserId });
                    if (viewer == null)
                    {
                        viewer = _getViewer.Invoke(null, new[] { NewWrapper(c.NameForRics, string.Empty, c.UserId) });
                        if (viewer == null) continue;
                        created++;
                    }

                    DateTime chatLocal = c.LastChatAt.ToLocalTime();
                    if (chatLocal > now) chatLocal = now;
                    var lastSeen = (DateTime)_lastSeen.GetValue(viewer, null);
                    if (chatLocal > lastSeen) _lastSeen.SetValue(viewer, chatLocal, null);

                    // Broadcaster steht nicht in der Liste; seine Rolle bleibt unberührt.
                    SetRoles(viewer, null, c.IsModerator, c.IsVip, c.IsSubscriber);
                    refreshed++;
                }
                catch (Exception ex)
                {
                    Log.Warning("[Twitch Companion] Aktivität nicht aufgefrischt: " + (ex.InnerException ?? ex).Message);
                }
            }
            return (refreshed, created);
        }

        /// <summary>
        /// RICS setzt beim Laden aller Zuschauer <c>LastSeen</c> auf „jetzt“ (<c>new Viewer(...)</c>), sodass in den
        /// ersten <c>MinutesForActive</c> Minuten alle bekannten Zuschauer Coins bekämen, auch Lurker. Die Mod setzt
        /// <c>LastSeen</c> aller Zuschauer ohne Nachricht in dieser Sitzung knapp hinter das Fenster zurück (E2).
        /// Einmal je Spielstart, im Hauptthread. Liefert die Anzahl.
        /// </summary>
        public static int ResetLoadedActivity()
        {
            if (!Available) return 0;
            Type viewers = _getViewer.DeclaringType;
            var all = AccessTools.Field(viewers, "All")?.GetValue(null) as System.Collections.IEnumerable;
            object lockObj = AccessTools.Field(viewers, "_lock")?.GetValue(null);
            if (all == null) return 0;

            int minutes = 30;
            try
            {
                object global = GetGlobalSettings();
                if (global != null && AccessTools.Field(global.GetType(), "MinutesForActive")?.GetValue(global) is int m && m > 0) minutes = m;
            }
            catch (Exception) { /* Vorgabe 30 */ }
            DateTime past = DateTime.Now.AddMinutes(-(minutes + 1));

            int count = 0;
            lock (lockObj ?? new object())
            {
                foreach (object viewer in all)
                {
                    if (viewer == null) continue;
                    PropertyInfo messages = AccessTools.Property(viewer.GetType(), "MessageCount");
                    if (messages != null && messages.GetValue(viewer, null) is int n && n > 0) continue;
                    if ((DateTime)_lastSeen.GetValue(viewer, null) > past)
                    {
                        _lastSeen.SetValue(viewer, past, null);
                        count++;
                    }
                }
            }
            return count;
        }

        private static void SetRoles(object viewer, bool? broadcaster, bool moderator, bool vip, bool subscriber)
        {
            // RICS liest Rollen sonst aus der TwitchLib-Nachricht, die es hier nicht gibt (Viewer.UpdateTwitchRoles).
            if (broadcaster.HasValue) SetBool(viewer, "IsBroadcaster", broadcaster.Value);
            SetBool(viewer, "IsModerator", moderator);
            SetBool(viewer, "IsVip", vip);
            SetBool(viewer, "IsSubscriber", subscriber);
        }

        private static void SetBool(object target, string property, bool value)
        {
            PropertyInfo p = AccessTools.Property(target.GetType(), property);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool)) p.SetValue(target, value);
        }
    }
}
