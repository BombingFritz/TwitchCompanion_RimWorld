// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – Stand für die Stream-Kartei bauen (Spec docs/specs/kartei.md).
// Vorarbeit: Die Mod baut den Stand regelmäßig und schreibt ihn auf Wunsch als Vorschau-Datei. An den Server geht er erst,
// wenn dessen Vertrag gemergt ist (Serveranforderung, Server-Spec stream-kartei.md, Stufe 2).

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using TwitchCompanion.RimWorld.Core;
using Verse;

namespace TwitchCompanion.RimWorld
{
    internal static class KarteiExport
    {
        private const string LogPrefix = "[Twitch Companion] ";
        private static DateTime? _lastBuildUtc;
        private static string _lastFingerprint;

        public static string LastResult { get; private set; }
        public static KarteiSnapshot Last { get; private set; }

        private static CompanionSettings Settings => TwitchCompanionMod.Settings;

        /// <summary>Ordner der Vorschau: Konfigurationsordner von RimWorld, Unterordner „TwitchCompanion“.</summary>
        public static string PreviewFolder => Path.Combine(GenFilePaths.ConfigFolderPath, "TwitchCompanion");

        /// <summary>Im Hauptthread. Baut höchstens alle <c>karteiIntervalMinutes</c> einen Stand; schreibt nur bei Änderung.</summary>
        public static void Tick()
        {
            if (!Settings.karteiPreview || Current.Game == null || Find.TickManager == null) return;
            DateTime now = DateTime.UtcNow;
            if (!KarteiSchedule.IsDue(_lastBuildUtc, now, TimeSpan.FromMinutes(Math.Max(1, Settings.karteiIntervalMinutes)))) return;
            _lastBuildUtc = now;
            BuildAndWrite(force: false);
        }

        /// <summary>Stand bauen und als Vorschau schreiben (Knopf in den Einstellungen: <paramref name="force"/>).</summary>
        public static void BuildAndWrite(bool force)
        {
            try
            {
                KarteiSnapshot s = Build();
                Last = s;
                string fingerprint = KarteiJson.Fingerprint(s);
                if (!force && fingerprint == _lastFingerprint) { LastResult = DateTime.Now.ToString("HH:mm") + ": unverändert"; return; }
                _lastFingerprint = fingerprint;
                Directory.CreateDirectory(PreviewFolder);
                // Vorschau ohne Twitch-IDs (forServer: false).
                File.WriteAllText(Path.Combine(PreviewFolder, "kartei-rimworld-pawns.json"), KarteiJson.Pawns(s, forServer: false));
                File.WriteAllText(Path.Combine(PreviewFolder, "kartei-rimworld-colony.json"), KarteiJson.Colony(s));
                LastResult = DateTime.Now.ToString("HH:mm") + ": " + s.Colonists.Count + " Kolonisten, " + s.Deceased.Count + " Verstorbene";
                Log.Message(LogPrefix + "Stream-Kartei: Vorschau geschrieben (" + LastResult + ").");
            }
            catch (Exception ex)
            {
                LastResult = "Fehler: " + ex.Message;
                Log.Warning(LogPrefix + "Stream-Kartei: Stand nicht gebaut: " + ex);
            }
        }

        // ── Stand aus dem Spiel ───────────────────────────────────────────

        public static KarteiSnapshot Build()
        {
            var s = new KarteiSnapshot { GameVersion = VersionControl.CurrentVersionStringWithRev, GeneratedAtUtc = DateTime.UtcNow };
            Dictionary<string, string> links = ViewerLinks();
            HashSet<string> privateIds = Bridge.PrivateUserIds;

            foreach (Pawn p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.OrderBy(x => x.Name?.ToStringShort))
            {
                KarteiPawn kp = FromPawn(p, links);
                if (kp == null || (kp.LinkedUserId != null && privateIds.Contains(kp.LinkedUserId))) continue; // !privat: nie auf die Seite
                s.Colonists.Add(kp);
            }
            foreach (Pawn p in DeadColonists().OrderBy(x => x.Name?.ToStringShort))
            {
                KarteiPawn kp = FromPawn(p, links);
                if (kp == null || (kp.LinkedUserId != null && privateIds.Contains(kp.LinkedUserId))) continue;
                s.Deceased.Add(kp);
            }
            s.Colony = ColonyStats(s);
            return s;
        }

        private static IEnumerable<Pawn> DeadColonists() =>
            PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
                .Where(p => p != null && p.Dead && p.RaceProps?.Humanlike == true && p.Faction == Faction.OfPlayer && p.Name != null)
                .Distinct();

        private static KarteiPawn FromPawn(Pawn p, Dictionary<string, string> links)
        {
            try
            {
                var kp = new KarteiPawn
                {
                    Name = p.Name.ToStringShort, // Spitzname = Hauptname im Spiel, bei Zuschauer-Figuren der Twitch-Name (Nutzer, 24.11.2026)
                    GenderLabel = p.gender.GetLabel().CapitalizeFirst(),
                    AgeBiological = p.ageTracker?.AgeBiologicalYears,
                    AgeChronological = p.ageTracker?.AgeChronologicalYears,
                    Xenotype = ModsConfig.BiotechActive ? p.genes?.XenotypeLabelCap : null,
                    StatusLabel = p.Dead ? "verstorben" : p.IsCaravanMember() ? "mit einer Karawane unterwegs" : p.IsPrisoner ? "gefangen" : "in der Kolonie",
                };
                if (p.Name is NameTriple n) { kp.NameFirst = n.First; kp.NameNick = n.Nick; kp.NameLast = n.Last; }
                if (p.story != null)
                {
                    kp.Title = p.story.TitleCap;
                    kp.Childhood = Story(p.story.Childhood, p);
                    kp.Adulthood = Story(p.story.Adulthood, p);
                    kp.Traits = p.story.traits?.allTraits?.Select(t => (string)t.LabelCap).ToList() ?? new List<string>();
                }
                if (p.skills != null)
                    foreach (SkillRecord r in p.skills.skills)
                        kp.Skills.Add(new KarteiSkill
                        {
                            Name = r.def.LabelCap,
                            Level = r.TotallyDisabled ? 0 : r.Level,
                            Passion = r.passion == Passion.Major ? "Leidenschaft" : r.passion == Passion.Minor ? "Interesse" : "keine",
                            Disabled = r.TotallyDisabled,
                        });
                if (p.health?.hediffSet != null)
                    foreach (Hediff h in p.health.hediffSet.hediffs.Where(h => h.Visible))
                        kp.Health.Add(new KarteiCondition { Label = h.LabelCap, Part = h.Part?.LabelCap });
                if (!p.Dead && p.needs?.mood != null) kp.Mood = (int)Math.Round(p.needs.mood.CurLevelPercentage * 100f);
                kp.Weapon = p.equipment?.Primary?.LabelCap;
                if (p.apparel != null) kp.Apparel = p.apparel.WornApparel.Select(a => (string)a.LabelCap).ToList();
                if (p.relations != null && !p.Dead)
                    foreach (Pawn other in p.relations.RelatedPawns.Where(o => o != null && o.Faction == Faction.OfPlayer && o.RaceProps?.Humanlike == true).Distinct())
                    {
                        PawnRelationDef rel = p.GetMostImportantRelation(other);
                        if (rel != null) kp.Relations.Add(new KarteiRelation { Label = rel.GetGenderSpecificLabelCap(other), Other = other.Name?.ToStringShort });
                    }
                if (p.records != null)
                {
                    kp.KillsHumanlike = (int)p.records.GetValue(RecordDefOf.KillsHumanlikes);
                    kp.KillsAnimals = (int)p.records.GetValue(RecordDefOf.KillsAnimals);
                    kp.DaysAsColonist = p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) / GenDate.TicksPerDay;
                }
                if (links.TryGetValue(p.ThingID, out string platformId))
                {
                    kp.ViewerLinked = true;
                    kp.LinkedUserId = platformId.Contains(":") ? platformId.Substring(platformId.IndexOf(':') + 1) : null;
                }
                return kp;
            }
            catch (Exception ex)
            {
                Log.Warning(LogPrefix + "Stream-Kartei: Akte übersprungen: " + ex.Message);
                return null;
            }
        }

        private static KarteiStory Story(BackstoryDef b, Pawn p) =>
            b == null ? null : new KarteiStory { Title = b.TitleCapFor(p.gender), Description = b.FullDescriptionFor(p).Resolve() };

        private static KarteiColony ColonyStats(KarteiSnapshot s)
        {
            var c = new KarteiColony
            {
                FactionName = Faction.OfPlayer?.Name,
                DaysPassed = GenDate.DaysPassed,
                Storyteller = Find.Storyteller?.def?.LabelCap,
                Difficulty = Find.Storyteller?.difficultyDef?.LabelCap,
                Colonists = s.Colonists.Count,
                Deceased = s.Deceased.Count,
                Mods = LoadedModManager.RunningModsListForReading.Where(m => !m.IsOfficialMod).Select(m => m.Name).ToList(),
            };
            Map home = Find.Maps?.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsCount).FirstOrDefault();
            if (home != null)
            {
                c.ColonyName = home.Parent?.LabelCap;
                c.Biome = home.Biome?.LabelCap;
                c.DateLabel = GenDate.DateFullStringAt(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(home.Tile));
                c.WealthTotal = home.wealthWatcher.WealthTotal;
                c.WealthItems = home.wealthWatcher.WealthItems;
                c.WealthBuildings = home.wealthWatcher.WealthBuildings;
                c.WealthPawns = home.wealthWatcher.WealthPawns;
                c.Prisoners = home.mapPawns.PrisonersOfColonyCount;
                c.Animals = home.mapPawns.SpawnedColonyAnimals.Count;
            }
            List<int> moods = s.Colonists.Where(p => p.Mood.HasValue).Select(p => p.Mood.Value).ToList();
            c.AverageMood = moods.Count > 0 ? (int?)Math.Round(moods.Average()) : null;
            if (Settings.karmaEnabled)
            {
                c.ColonyKarma = KarmaKeeper.ColonyValue;
                c.ColonyKarmaLabel = ColonyKarma.Word(c.ColonyKarma.Value);
            }
            return c;
        }

        /// <summary>Thing-ID des Pawns → Plattform-ID des Zuschauers („twitch:123“), aus der RICS-Zuordnung.</summary>
        private static Dictionary<string, string> ViewerLinks()
        {
            var result = new Dictionary<string, string>();
            Type manager = AccessTools.TypeByName("CAP_ChatInteractive.GameComponent_PawnAssignmentManager");
            object comp = manager == null ? null : AccessTools.Method(typeof(Game), "GetComponent", Type.EmptyTypes)?.MakeGenericMethod(manager).Invoke(Current.Game, null);
            if (comp == null || !(AccessTools.Field(manager, "viewerPawnAssignments")?.GetValue(comp) is IDictionary map)) return result;
            foreach (DictionaryEntry e in map)
                if (e.Key is string id && e.Value is string thing && QueueRules.IsPlatformId(id)) result[thing] = id;
            return result;
        }
    }
}
