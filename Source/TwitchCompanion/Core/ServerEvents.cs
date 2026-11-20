// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Daten des Servers (Spec des Servers „Chat-Brücke“, Teil A und B).

using System;
using System.Collections.Generic;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Ereignis <c>hello</c> nach dem Verbinden.</summary>
    public sealed class HelloInfo
    {
        public string Server;
        public string Version;
        public string Client;
    }

    /// <summary>Ereignis <c>privacy</c>: jemand hat <c>!privat</c> ein- oder ausgeschaltet.</summary>
    public sealed class PrivacyChange
    {
        public string UserId;
        public bool Private;
    }

    /// <summary>Antwort von <c>GET /api/twitch/channel</c>.</summary>
    public sealed class ChannelInfo
    {
        public string Category;
        public DateTime? CategoryChangedAt;
        public bool Live;
    }

    /// <summary>Eintrag aus <c>GET /api/chat/active</c>.</summary>
    public sealed class ActiveChatter
    {
        public string UserId;
        public string Login;
        public string DisplayName;
        public DateTime LastChatAt;
        public bool IsModerator;
        public bool IsVip;
        public bool IsSubscriber;

        public string NameForRics =>
            !string.IsNullOrEmpty(DisplayName) && string.Equals(DisplayName, Login, StringComparison.OrdinalIgnoreCase)
                ? DisplayName
                : Login;
    }

    /// <summary>
    /// Übersetzt JSON des Servers in Objekte. Unbekannte Zusatzfelder werden ignoriert; fehlen Pflichtfelder,
    /// ist das Ergebnis <c>null</c> (bzw. der Eintrag fehlt) statt geraten (AGENTS.md, „Schnittstellen zum Server“).
    /// </summary>
    public static class ServerEvents
    {
        public static HelloInfo ParseHello(string data)
        {
            var o = Json.TryParseObject(data);
            if (o == null) return null;
            return new HelloInfo
            {
                Server = Json.GetString(o, "server"),
                Version = Json.GetString(o, "version"),
                Client = Json.GetString(o, "client"),
            };
        }

        public static ChatLine ParseChat(string data)
        {
            var o = Json.TryParseObject(data);
            if (o == null) return null;
            var badges = Json.GetObject(o, "badges");
            var line = new ChatLine
            {
                MessageId = Json.GetString(o, "messageId"),
                UserId = Json.GetString(o, "userId"),
                Login = Json.GetString(o, "userLogin"),
                DisplayName = Json.GetString(o, "userName"),
                Text = Json.GetString(o, "text"),
                ReceivedAt = Json.GetTime(o, "at"),
                IsBroadcaster = Json.GetBool(badges, "broadcaster"),
                IsModerator = Json.GetBool(badges, "moderator"),
                IsVip = Json.GetBool(badges, "vip"),
                IsSubscriber = Json.GetBool(badges, "subscriber"),
                IsPrivate = Json.GetBool(o, "private"),
            };
            if (line.Login != null) line.Login = line.Login.Trim().ToLowerInvariant();
            return line.IsComplete ? line : null;
        }

        public static PrivacyChange ParsePrivacy(string data)
        {
            var o = Json.TryParseObject(data);
            string id = Json.GetString(o, "userId");
            bool? priv = Json.GetNullableBool(o, "private");
            if (string.IsNullOrEmpty(id) || priv == null) return null;
            return new PrivacyChange { UserId = id, Private = priv.Value };
        }

        public static ChannelInfo ParseChannel(string data)
        {
            var o = Json.TryParseObject(data);
            if (o == null || !o.ContainsKey("category")) return null;
            return new ChannelInfo
            {
                Category = Json.GetString(o, "category"),
                CategoryChangedAt = Json.GetTime(o, "categoryChangedAt"),
                Live = Json.GetBool(o, "live"),
            };
        }

        /// <summary>Liste aktiver Chatter; <c>null</c>, wenn die Antwort kein <c>chatters</c>-Feld hat.</summary>
        public static List<ActiveChatter> ParseActive(string data)
        {
            var o = Json.TryParseObject(data);
            var list = Json.GetArray(o, "chatters");
            if (list == null) return null;
            var result = new List<ActiveChatter>(list.Count);
            foreach (object item in list)
            {
                var c = item as Dictionary<string, object>;
                string id = Json.GetString(c, "id");
                string login = Json.GetString(c, "login");
                DateTime? at = Json.GetTime(c, "lastChatAt");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(login) || at == null) continue;
                result.Add(new ActiveChatter
                {
                    UserId = id,
                    Login = login.Trim().ToLowerInvariant(),
                    DisplayName = Json.GetString(c, "name"),
                    LastChatAt = at.Value,
                    IsModerator = Json.GetBool(c, "moderator"),
                    IsVip = Json.GetBool(c, "vip"),
                    IsSubscriber = Json.GetBool(c, "subscriber"),
                });
            }
            return result;
        }

        /// <summary>Wartezeit aus einer 429-Antwort (<c>retryAfterMs</c>), sonst <paramref name="fallbackMs"/>; begrenzt auf 100 ms bis 10 s.</summary>
        public static int ParseRetryAfterMs(string data, int fallbackMs)
        {
            double? ms = Json.GetNumber(Json.TryParseObject(data), "retryAfterMs");
            int value = ms.HasValue ? (int)Math.Ceiling(ms.Value) : fallbackMs;
            return Math.Max(100, Math.Min(10000, value));
        }

        /// <summary>Feld <c>sent</c> einer 200-Antwort; <c>null</c>, wenn es fehlt.</summary>
        public static bool? ParseSent(string data) => Json.GetNullableBool(Json.TryParseObject(data), "sent");

        public static string BuildSendBody(string feature, string message)
            => "{\"feature\":" + Json.Quote(feature) + ",\"message\":" + Json.Quote(message) + "}";
    }
}
