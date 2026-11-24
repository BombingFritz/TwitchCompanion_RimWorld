// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using TwitchCompanion.RimWorld.Core;
using Xunit;

namespace TwitchCompanion.RimWorld.Tests
{
    public class KarteiTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 24, 20, 0, 0, DateTimeKind.Utc);

        private static KarteiSnapshot Sample(DateTime at)
        {
            var s = new KarteiSnapshot { GameVersion = "1.6.4871", GeneratedAtUtc = at };
            s.Colonists.Add(new KarteiPawn
            {
                Name = "Kiwi", NameFirst = "Karla", NameNick = "Kiwi", NameLast = "Weber", Title = "Gärtnerin", GenderLabel = "Weiblich",
                AgeBiological = 31, AgeChronological = 95, Xenotype = "Baseliner", StatusLabel = "in der Kolonie", Mood = 64,
                Childhood = new KarteiStory { Title = "Bauernkind", Description = "Zeile 1\nZeile \"2\"" },
                Traits = new List<string> { "Fleißig" },
                Skills = new List<KarteiSkill> { new KarteiSkill { Name = "Pflanzen", Level = 9, Passion = "Leidenschaft" } },
                Health = new List<KarteiCondition> { new KarteiCondition { Label = "Narbe", Part = "Linker Arm" } },
                Weapon = "Revolver", Apparel = new List<string> { "Hemd" },
                Relations = new List<KarteiRelation> { new KarteiRelation { Label = "Schwester", Other = "Rolf" } },
                KillsHumanlike = 2, KillsAnimals = 5, DaysAsColonist = 12.345, ViewerLinked = true, LinkedUserId = "12345",
            });
            s.Deceased.Add(new KarteiPawn { Name = "Rolf", StatusLabel = "verstorben", DiedLabel = "5. Frühsommer 5501" });
            s.Colony = new KarteiColony
            {
                ColonyName = "Spargelhof", FactionName = "Neue Siedler", DateLabel = "7. Aprimay 5501", DaysPassed = 37,
                Storyteller = "Cassandra", Difficulty = "Raue Welt", Biome = "Gemäßigter Wald",
                WealthTotal = 13166.4f, WealthItems = 6000f, WealthBuildings = 5000f, WealthPawns = 2166.4f,
                Colonists = 1, Prisoners = 0, Animals = 3, Deceased = 1, AverageMood = 64, ColonyKarma = 12.6f, ColonyKarmaLabel = "ausgeglichen",
                Mods = new List<string> { "RICS", "Twitch Companion" },
            };
            return s;
        }

        [Fact]
        public void PersonalaktenSindGueltigesJson()
        {
            var o = Json.TryParseObject(KarteiJson.Pawns(Sample(T0), forServer: false));
            Assert.NotNull(o);
            Assert.Equal(1, Json.GetNumber(o, "schemaVersion"));
            Assert.Equal("rimworld", Json.GetString(o, "game"));
            Assert.Equal("2026-11-24T20:00:00.000Z", Json.GetString(o, "generatedAt"));
            var kiwi = (Dictionary<string, object>)Json.GetArray(o, "colonists")[0];
            Assert.Equal("Kiwi", Json.GetString(kiwi, "name"));
            Assert.Equal("Zeile 1\nZeile \"2\"", Json.GetString(Json.GetObject(kiwi, "childhood"), "description"));
            Assert.Null(Json.GetObject(kiwi, "adulthood"));
            Assert.Equal(12.3, Json.GetNumber(kiwi, "daysAsColonist"));
            Assert.True(Json.GetBool(kiwi, "viewerLinked"));
            Assert.Single(Json.GetArray(o, "deceased"));
            Assert.Null(Json.GetArray(o, "fallen"));
        }

        [Fact]
        public void TwitchIdNurFuerDenServer()
        {
            KarteiSnapshot s = Sample(T0);
            Assert.DoesNotContain("12345", KarteiJson.Pawns(s, forServer: false));
            Assert.DoesNotContain("linkedUserId", KarteiJson.Pawns(s, forServer: false));
            Assert.Contains("\"linkedUserId\":\"12345\"", KarteiJson.Pawns(s, forServer: true));
            Assert.DoesNotContain("12345", KarteiJson.Colony(s));
        }

        [Fact]
        public void KolonieStatistikIstGueltigesJson()
        {
            var o = Json.TryParseObject(KarteiJson.Colony(Sample(T0)));
            Assert.NotNull(o);
            Assert.Equal("Spargelhof", Json.GetString(o, "colonyName"));
            Assert.Equal(13166, Json.GetNumber(Json.GetObject(o, "wealth"), "total"));
            Assert.Equal(13, Json.GetNumber(Json.GetObject(o, "colonyKarma"), "value"));
            Assert.Equal(2, Json.GetArray(o, "mods").Count);
            var s = Sample(T0);
            s.Colony.ColonyKarma = null;
            Assert.Null(Json.GetObject(Json.TryParseObject(KarteiJson.Colony(s)), "colonyKarma"));
        }

        [Fact]
        public void FingerabdruckOhneZeitstempel()
        {
            Assert.Equal(KarteiJson.Fingerprint(Sample(T0)), KarteiJson.Fingerprint(Sample(T0.AddMinutes(30))));
            var changed = Sample(T0);
            changed.Colonists[0].Mood = 65;
            Assert.NotEqual(KarteiJson.Fingerprint(Sample(T0)), KarteiJson.Fingerprint(changed));
        }

        [Fact]
        public void Takt()
        {
            var ten = TimeSpan.FromMinutes(10);
            Assert.True(KarteiSchedule.IsDue(null, T0, ten));
            Assert.False(KarteiSchedule.IsDue(T0, T0.AddMinutes(9), ten));
            Assert.True(KarteiSchedule.IsDue(T0, T0.AddMinutes(10), ten));
        }
    }
}
