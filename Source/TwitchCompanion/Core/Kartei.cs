// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Daten für die Stream-Kartei (Personalakten und Kolonie-Statistik; Spec docs/specs/kartei.md).
// Format ist ein Vorschlag der Mod (Server-Spec stream-kartei.md, Stufe 2: „Inhalt und Felder entscheiden Mod und Nutzer“).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TwitchCompanion.RimWorld.Core
{
    public sealed class KarteiSkill
    {
        public string Name;
        public int Level;
        /// <summary>„keine“, „Interesse“, „Leidenschaft“.</summary>
        public string Passion;
        public bool Disabled;
    }

    public sealed class KarteiStory
    {
        public string Title;
        public string Description;
    }

    public sealed class KarteiCondition
    {
        public string Label;
        public string Part;
    }

    public sealed class KarteiRelation
    {
        public string Label;
        public string Other;
    }

    /// <summary>Personalakte einer Figur. Nur Spieldaten, keine Twitch-Daten (Server-Spec stream-kartei.md).</summary>
    public sealed class KarteiPawn
    {
        public string Name, NameFirst, NameNick, NameLast;
        public string Title;
        public string GenderLabel;
        public int? AgeBiological, AgeChronological;
        public string Xenotype;
        public KarteiStory Childhood, Adulthood;
        public List<string> Traits = new List<string>();
        public List<KarteiSkill> Skills = new List<KarteiSkill>();
        public List<KarteiCondition> Health = new List<KarteiCondition>();
        /// <summary>Stimmung in Prozent, <c>null</c> wenn unbekannt (z. B. verstorben).</summary>
        public int? Mood;
        public string StatusLabel;
        public string Weapon;
        public List<string> Apparel = new List<string>();
        public List<KarteiRelation> Relations = new List<KarteiRelation>();
        public int KillsHumanlike, KillsAnimals;
        public double DaysAsColonist;
        /// <summary>Gehört zu einem Zuschauer (ohne zu sagen, zu welchem).</summary>
        public bool ViewerLinked;
        /// <summary>Nur für den Server: Twitch-ID des verknüpften Zuschauers, damit er bei <c>!privat</c> ausblenden kann. Wird nie veröffentlicht.</summary>
        public string LinkedUserId;
        /// <summary>Spieldatum des Todes als Text, nur bei Verstorbenen.</summary>
        public string DiedLabel;
    }

    public sealed class KarteiColony
    {
        public string ColonyName, FactionName;
        public string DateLabel;
        public int DaysPassed;
        public string Storyteller, Difficulty, Biome;
        public float WealthTotal, WealthItems, WealthBuildings, WealthPawns;
        public int Colonists, Prisoners, Animals, Deceased;
        public int? AverageMood;
        public float? ColonyKarma;
        public string ColonyKarmaLabel;
        public List<string> Mods = new List<string>();
    }

    public sealed class KarteiSnapshot
    {
        public const int SchemaVersion = 1;
        public string GameVersion;
        public DateTime GeneratedAtUtc;
        public List<KarteiPawn> Colonists = new List<KarteiPawn>();
        /// <summary>Verstorbene (Nutzer, 24.11.2026: „Verstorbene“, auf der Seite „Wir gedenken: …“).</summary>
        public List<KarteiPawn> Deceased = new List<KarteiPawn>();
        public KarteiColony Colony = new KarteiColony();
    }

    /// <summary>JSON für den Server (Vorschlag) und Vergleich, ob sich etwas geändert hat.</summary>
    public static class KarteiJson
    {
        /// <summary>
        /// Personalakten (<c>rimworld/pawns.json</c>). <paramref name="forServer"/>: mit <c>linkedUserId</c> (nur lokal an
        /// den Server, der sie vor dem Veröffentlichen entfernt); sonst ohne.
        /// </summary>
        public static string Pawns(KarteiSnapshot s, bool forServer, bool withTimestamp = true)
        {
            var w = new Writer();
            w.Open();
            w.Num("schemaVersion", KarteiSnapshot.SchemaVersion).Str("game", "rimworld").Str("gameVersion", s.GameVersion);
            if (withTimestamp) w.Str("generatedAt", Iso(s.GeneratedAtUtc));
            w.Arr("colonists", s.Colonists, p => PawnObject(w, p, forServer));
            w.Arr("deceased", s.Deceased, p => PawnObject(w, p, forServer));
            return w.Close();
        }

        /// <summary>Kolonie-Statistik für die Unterseite (<c>rimworld/colony.json</c>).</summary>
        public static string Colony(KarteiSnapshot s, bool withTimestamp = true)
        {
            KarteiColony c = s.Colony;
            var w = new Writer();
            w.Open();
            w.Num("schemaVersion", KarteiSnapshot.SchemaVersion).Str("game", "rimworld").Str("gameVersion", s.GameVersion);
            if (withTimestamp) w.Str("generatedAt", Iso(s.GeneratedAtUtc));
            w.Str("colonyName", c.ColonyName).Str("factionName", c.FactionName).Str("dateLabel", c.DateLabel).Num("daysPassed", c.DaysPassed)
             .Str("storyteller", c.Storyteller).Str("difficulty", c.Difficulty).Str("biome", c.Biome);
            w.Key("wealth"); w.Open();
            w.Num("total", Round(c.WealthTotal)).Num("items", Round(c.WealthItems)).Num("buildings", Round(c.WealthBuildings)).Num("pawns", Round(c.WealthPawns));
            w.CloseInner();
            w.Num("colonists", c.Colonists).Num("prisoners", c.Prisoners).Num("animals", c.Animals).Num("deceased", c.Deceased)
             .NumOrNull("averageMood", c.AverageMood);
            w.Key("colonyKarma");
            if (c.ColonyKarma.HasValue) { w.Open(); w.Num("value", Round(c.ColonyKarma.Value)).Str("label", c.ColonyKarmaLabel); w.CloseInner(); }
            else w.Raw("null");
            w.StrArr("mods", c.Mods);
            return w.Close();
        }

        private static void PawnObject(Writer w, KarteiPawn p, bool forServer)
        {
            w.Open();
            w.Str("name", p.Name).Str("nameFirst", p.NameFirst).Str("nameNick", p.NameNick).Str("nameLast", p.NameLast)
             .Str("title", p.Title).Str("genderLabel", p.GenderLabel).NumOrNull("ageBiological", p.AgeBiological).NumOrNull("ageChronological", p.AgeChronological)
             .Str("xenotype", p.Xenotype).Str("statusLabel", p.StatusLabel).NumOrNull("mood", p.Mood);
            Story(w, "childhood", p.Childhood);
            Story(w, "adulthood", p.Adulthood);
            w.StrArr("traits", p.Traits);
            w.Arr("skills", p.Skills, k => { w.Open(); w.Str("name", k.Name).Num("level", k.Level).Str("passion", k.Passion).Bool("disabled", k.Disabled); w.CloseInner(); });
            w.Arr("health", p.Health, h => { w.Open(); w.Str("label", h.Label).Str("part", h.Part); w.CloseInner(); });
            w.Str("weapon", p.Weapon).StrArr("apparel", p.Apparel);
            w.Arr("relations", p.Relations, r => { w.Open(); w.Str("label", r.Label).Str("other", r.Other); w.CloseInner(); });
            w.Num("killsHumanlike", p.KillsHumanlike).Num("killsAnimals", p.KillsAnimals).Num("daysAsColonist", Math.Round(p.DaysAsColonist, 1))
             .Bool("viewerLinked", p.ViewerLinked).Str("diedLabel", p.DiedLabel);
            if (forServer) w.Str("linkedUserId", p.LinkedUserId);
            w.CloseInner();
        }

        private static void Story(Writer w, string key, KarteiStory s)
        {
            w.Key(key);
            if (s == null) { w.Raw("null"); return; }
            w.Open(); w.Str("title", s.Title).Str("description", s.Description); w.CloseInner();
        }

        /// <summary>ENTWURF: Körper für <c>POST /api/kartei/rimworld</c> (Vorschlag, Server-Issue #98): beide Teile, Personalakten mit <c>linkedUserId</c>.</summary>
        public static string ServerBody(KarteiSnapshot s) => "{\"pawns\":" + Pawns(s, forServer: true) + ",\"colony\":" + Colony(s) + "}";

        /// <summary>Fingerabdruck ohne Zeitstempel: gleich, wenn sich am Inhalt nichts geändert hat.</summary>
        public static string Fingerprint(KarteiSnapshot s)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(Pawns(s, true, false) + "\n" + Colony(s, false));
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).Substring(0, 16);
            }
        }

        private static string Iso(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static double Round(float v) => Math.Round(v, 0);

        /// <summary>Kleiner JSON-Schreiber mit Kommasetzung; Texte über <see cref="Json.Quote"/>.</summary>
        private sealed class Writer
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private readonly Stack<bool> _first = new Stack<bool>();

            public void Open() { _sb.Append('{'); _first.Push(true); }
            public void CloseInner() { _sb.Append('}'); _first.Pop(); }
            public string Close() { CloseInner(); return _sb.ToString(); }

            public void Key(string k)
            {
                if (_first.Count > 0)
                {
                    if (!_first.Peek()) _sb.Append(',');
                    _first.Pop(); _first.Push(false);
                }
                _sb.Append(Json.Quote(k)).Append(':');
            }

            public void Raw(string v) => _sb.Append(v);
            public Writer Str(string k, string v) { Key(k); _sb.Append(v == null ? "null" : Json.Quote(v)); return this; }
            public Writer Num(string k, double v) { Key(k); _sb.Append(v.ToString("0.###", CultureInfo.InvariantCulture)); return this; }
            public Writer NumOrNull(string k, double? v) { Key(k); _sb.Append(v.HasValue ? v.Value.ToString("0.###", CultureInfo.InvariantCulture) : "null"); return this; }
            public Writer Bool(string k, bool v) { Key(k); _sb.Append(v ? "true" : "false"); return this; }

            public Writer StrArr(string k, IEnumerable<string> values)
            {
                Key(k);
                _sb.Append('[').Append(string.Join(",", (values ?? Enumerable.Empty<string>()).Select(Json.Quote))).Append(']');
                return this;
            }

            public Writer Arr<T>(string k, IEnumerable<T> items, Action<T> write)
            {
                Key(k);
                _sb.Append('[');
                bool first = true;
                foreach (T item in items ?? Enumerable.Empty<T>())
                {
                    if (!first) _sb.Append(',');
                    first = false;
                    write(item);
                }
                _sb.Append(']');
                return this;
            }
        }
    }

    /// <summary>Wann die Mod einen neuen Stand baut: höchstens alle <c>interval</c>, und nur weiter, wenn er sich geändert hat.</summary>
    public static class KarteiSchedule
    {
        public static bool IsDue(DateTime? lastBuildUtc, DateTime nowUtc, TimeSpan interval) =>
            !lastBuildUtc.HasValue || nowUtc - lastBuildUtc.Value >= interval;
    }
}
