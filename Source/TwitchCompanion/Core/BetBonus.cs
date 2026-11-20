// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Pfennig-Bonus für gewonnene Twitch-Wetten (Feature-Issue #11).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TwitchCompanion.RimWorld.Core
{
    public sealed class PredictionPayout
    {
        public string UserId;
        public string Login;
        public string Name;
        public long PointsWon;
    }

    /// <summary>Letzter Stand einer Wette aus <c>GET /api/twitch/prediction/current</c> (Server-Spec <c>wetten-abstimmungen.md</c>, Schritt 1).</summary>
    public sealed class PredictionResult
    {
        public string Id;
        public string Status;
        public string WinningOutcomeId;
        /// <summary>Ende der Wette laut Twitch (UTC), <c>null</c> wenn unbekannt.</summary>
        public DateTime? EndedAt;
        /// <summary>Gewinner laut Twitch: nur die bis zu 10 Tipper mit dem höchsten Einsatz je Ausgang.</summary>
        public List<PredictionPayout> Winners = new List<PredictionPayout>();

        public bool IsResolved => string.Equals(Status, "RESOLVED", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(WinningOutcomeId);
    }

    public sealed class BonusAward
    {
        public string UserId;
        public string Login;
        public string Name;
        public long PointsWon;
        public int Pfennig;
    }

    public static class BetBonus
    {
        /// <summary>1000 gewonnene Kanalpunkte (ganze Auszahlung) = eine Zuschaustunde (Nutzer, 20.11.2026).</summary>
        public const int PointsPerHour = 1000;

        public static PredictionResult Parse(string json)
        {
            Dictionary<string, object> o = Json.TryParseObject(json);
            if (o == null) return null;
            var r = new PredictionResult
            {
                Id = Json.GetString(o, "predictionId"),
                Status = Json.GetString(o, "status"),
                WinningOutcomeId = Json.GetString(o, "winningOutcomeId"),
                EndedAt = Json.GetTime(o, "endedAt"),
            };
            if (string.IsNullOrEmpty(r.Id)) return r;
            foreach (object oc in Json.GetArray(o, "outcomes") ?? new List<object>())
            {
                if (!(oc is Dictionary<string, object> outcome) || Json.GetString(outcome, "id") != r.WinningOutcomeId) continue;
                foreach (object tp in Json.GetArray(outcome, "topPredictors") ?? new List<object>())
                {
                    if (!(tp is Dictionary<string, object> p)) continue;
                    r.Winners.Add(new PredictionPayout
                    {
                        UserId = Json.GetString(p, "userId"),
                        Login = Json.GetString(p, "userLogin"),
                        Name = Json.GetString(p, "userName"),
                        PointsWon = (long)(Json.GetNumber(p, "channelPointsWon") ?? 0),
                    });
                }
            }
            return r;
        }

        /// <summary>Bonus je Gewinner: Auszahlung / 1000 × Pfennig je Zuschaustunde, gerundet; ohne <c>!privat</c> und ohne ID.</summary>
        /// <summary>
        /// Nur frisch beendete Wetten zählen. Der Server liefert auch eine längst beendete letzte Wette; ohne diese Grenze
        /// gäbe es beim ersten Abruf nach dem Start einen Bonus für eine alte Wette (Befund vom 21.11.2026).
        /// </summary>
        public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

        public static bool IsFresh(PredictionResult r, DateTime nowUtc) =>
            r?.EndedAt != null && nowUtc - r.EndedAt.Value.ToUniversalTime() <= MaxAge;

        public static List<BonusAward> Compute(PredictionResult result, int coinsPerHour, ICollection<string> privateUserIds)
        {
            var awards = new List<BonusAward>();
            if (result == null || !result.IsResolved) return awards;
            foreach (PredictionPayout w in result.Winners)
            {
                if (string.IsNullOrEmpty(w.UserId) || string.IsNullOrEmpty(w.Login) || w.PointsWon <= 0) continue;
                if (privateUserIds != null && privateUserIds.Contains(w.UserId)) continue;
                int pfennig = (int)Math.Round((double)w.PointsWon / PointsPerHour * coinsPerHour);
                if (pfennig <= 0) continue;
                awards.Add(new BonusAward { UserId = w.UserId, Login = w.Login, Name = w.Name, PointsWon = w.PointsWon, Pfennig = pfennig });
            }
            return awards.OrderByDescending(a => a.Pfennig).ToList();
        }

        public static string Announcement(IList<BonusAward> awards, string currency)
        {
            if (awards == null || awards.Count == 0) return null;
            string unit = string.IsNullOrWhiteSpace(currency) ? "Pfennig" : currency.Trim();
            return "Wettbonus: " + string.Join(", ", awards.Select(a =>
                "@" + a.Login + " +" + a.Pfennig.ToString("N0", CultureInfo.GetCultureInfo("de-DE")) + " " + unit));
        }
    }
}
