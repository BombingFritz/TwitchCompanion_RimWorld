// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Abstimmung starten und Raids empfangen über den Server
// (Server-Spec chat-bruecke.md, Teil C und D, Server-PR #83, main cc6da34).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Antwort auf <c>POST /api/twitch/poll/start</c>.</summary>
    public sealed class PollStartResult
    {
        public bool Ok;
        public string PollId;
        public DateTime? StartedAt;
        public DateTime? EndsAt;
        /// <summary><c>invalid</c>, <c>forbidden</c>, <c>missing_scope</c>, <c>not_eligible</c>, <c>poll_active</c>, <c>too_fast</c>,
        /// <c>twitch_rejected</c>, <c>twitch_error</c>, <c>not_logged_in</c>; eigene: <c>unreachable</c>, <c>not_supported</c>, <c>unreadable</c>.</summary>
        public string Reason;
        public string Error;
        public int? RetryAfterMs;
        public DateTime? ActiveEndsAt;
        public int HttpStatus;
    }

    public sealed class PollChoiceResult
    {
        public string Id;
        public string Title;
        public int Votes;
        public int ChannelPointsVotes;
    }

    /// <summary>Ereignis <c>poll_result</c> im Strom <c>/api/chat/stream</c>.</summary>
    public sealed class PollResult
    {
        public string PollId;
        public string Title;
        public string Status;
        public List<PollChoiceResult> Choices = new List<PollChoiceResult>();
        public int TotalVotes;
        public List<string> WinnerTitles = new List<string>();
        public DateTime? EndedAt;

        public bool IsCompleted => string.Equals(Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ereignis <c>raid</c>: eingehender Raid, genau einmal je Raid.</summary>
    public sealed class RaidInfo
    {
        public string Id;
        public DateTime? At;
        public string FromId;
        public string FromLogin;
        public string FromName;
        public int Viewers;

        /// <summary>Name für das Spiel: Anzeigename, wenn er sich nur in der Schreibweise vom Login unterscheidet, sonst Login.</summary>
        public string NameForGame =>
            !string.IsNullOrEmpty(FromName) && string.Equals(FromName, FromLogin, StringComparison.OrdinalIgnoreCase) ? FromName : FromLogin;
    }

    public static class ConsumerPolls
    {
        public static string BuildStartBody(string client, VoteRound round)
        {
            var sb = new StringBuilder();
            sb.Append("{\"client\":").Append(Json.Quote(client))
              .Append(",\"title\":").Append(Json.Quote(VoteRound.Shorten(round.Title, VoteRound.TitleMax)))
              .Append(",\"choices\":[").Append(string.Join(",", round.OptionTitles.Select(Json.Quote))).Append(']')
              .Append(",\"durationS\":").Append(((int)Math.Round((round.EndsUtc - round.StartedUtc).TotalSeconds)).ToString(CultureInfo.InvariantCulture));
            if (round.ChannelPointsPerVote > 0)
                sb.Append(",\"channelPointsPerVote\":").Append(round.ChannelPointsPerVote.ToString(CultureInfo.InvariantCulture));
            return sb.Append('}').ToString();
        }

        /// <summary>Wertet die Antwort aus; Status 0 heißt „keine Antwort“.</summary>
        public static PollStartResult ParseStart(int status, string body)
        {
            var r = new PollStartResult { HttpStatus = status };
            if (status == 0) { r.Reason = "unreachable"; return r; }
            Dictionary<string, object> o = Json.TryParseObject(body);
            if (status == 404 && (o == null || !o.ContainsKey("reason"))) { r.Reason = "not_supported"; return r; }
            if (o == null) { r.Reason = "unreadable"; r.Error = "HTTP " + status; return r; }
            r.Ok = Json.GetBool(o, "ok") && status == 200;
            r.PollId = Json.GetString(o, "pollId");
            r.StartedAt = Json.GetTime(o, "startedAt");
            r.EndsAt = Json.GetTime(o, "endsAt");
            r.Reason = Json.GetString(o, "reason");
            r.Error = Json.GetString(o, "error");
            double? retry = Json.GetNumber(o, "retryAfterMs");
            r.RetryAfterMs = retry.HasValue ? (int?)Math.Max(0, (int)Math.Ceiling(retry.Value)) : null;
            r.ActiveEndsAt = Json.GetTime(o, "endsAt");
            if (r.Ok && string.IsNullOrEmpty(r.PollId)) { r.Ok = false; r.Reason = "unreadable"; r.Error = "pollId fehlt"; }
            if (!r.Ok && string.IsNullOrEmpty(r.Reason)) r.Reason = "unreadable";
            return r;
        }

        public static PollResult ParseResult(string data)
        {
            Dictionary<string, object> o = Json.TryParseObject(data);
            if (o == null) return null;
            var r = new PollResult
            {
                PollId = Json.GetString(o, "pollId"),
                Title = Json.GetString(o, "title"),
                Status = Json.GetString(o, "status"),
                TotalVotes = (int)(Json.GetNumber(o, "totalVotes") ?? 0),
                EndedAt = Json.GetTime(o, "endedAt"),
            };
            if (string.IsNullOrEmpty(r.PollId) || string.IsNullOrEmpty(r.Status)) return null;
            foreach (object c in Json.GetArray(o, "choices") ?? new List<object>())
                if (c is Dictionary<string, object> d)
                    r.Choices.Add(new PollChoiceResult
                    {
                        Id = Json.GetString(d, "id"),
                        Title = Json.GetString(d, "title"),
                        Votes = (int)(Json.GetNumber(d, "votes") ?? 0),
                        ChannelPointsVotes = (int)(Json.GetNumber(d, "channelPointsVotes") ?? 0),
                    });
            foreach (object w in Json.GetArray(o, "winners") ?? new List<object>())
                if (w is Dictionary<string, object> d && Json.GetString(d, "title") is string t) r.WinnerTitles.Add(t);
            return r;
        }

        public static RaidInfo ParseRaid(string data)
        {
            Dictionary<string, object> o = Json.TryParseObject(data);
            if (o == null) return null;
            var r = new RaidInfo
            {
                Id = Json.GetString(o, "id"),
                At = Json.GetTime(o, "at"),
                FromId = Json.GetString(o, "fromId"),
                FromLogin = Json.GetString(o, "fromLogin"),
                FromName = Json.GetString(o, "fromName"),
                Viewers = (int)(Json.GetNumber(o, "viewers") ?? 0),
            };
            return string.IsNullOrEmpty(r.Id) || string.IsNullOrEmpty(r.FromLogin) ? null : r;
        }

        /// <summary>Kurzer deutscher Text zu einer Ablehnung, für Einblendung und Log.</summary>
        public static string Describe(PollStartResult r)
        {
            switch (r?.Reason)
            {
                case "poll_active": return "Es läuft schon eine Twitch-Abstimmung.";
                case "missing_scope": return "Dem Server fehlt die Berechtigung channel:manage:polls (Scope ergänzen, dann /login).";
                case "not_eligible": return "Twitch erlaubt dem Kanal keine Abstimmungen.";
                case "not_logged_in": return "Der Server ist nicht bei Twitch angemeldet.";
                case "too_fast": return "Zu schnell nach dem letzten Startversuch.";
                case "invalid": return "Server lehnt die Abstimmung ab: " + r.Error;
                case "twitch_rejected": return "Twitch lehnt die Abstimmung ab: " + r.Error;
                case "twitch_error": return "Twitch-Fehler: " + r.Error;
                case "unreachable": return "Server nicht erreichbar.";
                case "not_supported": return "Der Server kann noch keine Abstimmungen starten (Stand vor Server-PR #83).";
                default: return "Abstimmung nicht gestartet: " + (r?.Reason ?? "unbekannt") + (string.IsNullOrEmpty(r?.Error) ? string.Empty : " – " + r.Error);
            }
        }

        /// <summary>Wann der nächste Versuch sinnvoll ist, oder <c>null</c> für „erst in der nächsten regulären Runde“.</summary>
        public static TimeSpan? RetryAfter(PollStartResult r, DateTime nowUtc)
        {
            switch (r?.Reason)
            {
                case "poll_active":
                    TimeSpan untilEnd = r.ActiveEndsAt.HasValue ? r.ActiveEndsAt.Value - nowUtc : TimeSpan.Zero;
                    return untilEnd > TimeSpan.Zero ? untilEnd + TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(2);
                case "too_fast":
                    return TimeSpan.FromMilliseconds(Math.Max(1000, r.RetryAfterMs ?? 5000));
                case "unreachable":
                case "twitch_error":
                    return TimeSpan.FromMinutes(2);
                default:
                    return null;
            }
        }
    }
}

namespace TwitchCompanion.RimWorld.Core
{
    /// <summary>Wer einem Twitch-Raid automatisch beitritt (Server-Issue #80, Kommentar vom 20.11.2026).</summary>
    public static class RaidJoiners
    {
        /// <summary>Plaudernde der letzten Minuten vor dem Raid; ohne den raidenden Kanal, ohne doppelte, höchstens <paramref name="max"/>.</summary>
        public static List<string> Select(IEnumerable<ActiveChatter> chatters, RaidInfo raid, DateTime nowUtc, TimeSpan window, int max = 50)
        {
            var names = new List<string>();
            foreach (ActiveChatter c in (chatters ?? Enumerable.Empty<ActiveChatter>()).OrderByDescending(c => c.LastChatAt))
            {
                if (c == null || string.IsNullOrEmpty(c.Login) || nowUtc - c.LastChatAt > window) continue;
                if (raid != null && string.Equals(c.Login, raid.FromLogin, StringComparison.OrdinalIgnoreCase)) continue;
                string name = c.NameForRics;
                if (names.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                names.Add(name);
                if (names.Count >= max) break;
            }
            return names;
        }
    }
}
