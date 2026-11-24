// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – erfundene Beispieldaten für die Stream-Kartei (Dummys für Website und Server, docs/kartei-beispiel/).
// Alle Namen sind ausgedacht; kein Bezug zu echten Zuschauern.

using System;
using System.Collections.Generic;

namespace TwitchCompanion.RimWorld.Core
{
    public static class KarteiBeispiel
    {
        public static readonly DateTime GeneratedAtUtc = new DateTime(2026, 11, 24, 20, 30, 0, DateTimeKind.Utc);

        private static KarteiSkill S(string name, int level, string passion = "keine", bool disabled = false) =>
            new KarteiSkill { Name = name, Level = level, Passion = passion, Disabled = disabled };

        private static List<KarteiSkill> Skills(params KarteiSkill[] skills) => new List<KarteiSkill>(skills);

        public static KarteiSnapshot Snapshot()
        {
            var s = new KarteiSnapshot { GameVersion = "1.6.4871 rev591", GeneratedAtUtc = GeneratedAtUtc };
            s.Colonists.Add(new KarteiPawn
            {
                Name = "Spargelkönig", NameFirst = "Herbert", NameNick = "Spargelkönig", NameLast = "Kranich",
                Title = "Feldarbeiter", GenderLabel = "Männlich", AgeBiological = 42, AgeChronological = 117, Xenotype = "Baseliner",
                StatusLabel = "in der Kolonie", Mood = 71,
                Childhood = new KarteiStory { Title = "Bauernkind", Description = "Herbert wuchs auf einer kleinen Farm auf und lernte früh, mit Saatgut und Wetter umzugehen." },
                Adulthood = new KarteiStory { Title = "Feldarbeiter", Description = "Jahrelang bestellte Herbert fremde Felder für wenig Lohn." },
                Traits = new List<string> { "Fleißig", "Optimist" },
                Skills = Skills(S("Schießen", 4), S("Nahkampf", 3), S("Bauen", 6), S("Bergbau", 5), S("Kochen", 7, "Interesse"), S("Pflanzen", 13, "Leidenschaft"),
                    S("Tiere", 6), S("Handwerk", 2), S("Kunst", 0), S("Medizin", 3), S("Sozial", 5), S("Intellektuell", 1)),
                Health = new List<KarteiCondition> { new KarteiCondition { Label = "Narbe (Schnitt)", Part = "Linke Hand" } },
                Weapon = "Ikwa (gut)", Apparel = new List<string> { "Hemd aus Baumwolle", "Hose aus Baumwolle", "Cowboyhut" },
                Relations = new List<KarteiRelation> { new KarteiRelation { Label = "Ehefrau", Other = "Möhrchen" } },
                KillsHumanlike = 3, KillsAnimals = 11, DaysAsColonist = 37.2, ViewerLinked = true, LinkedUserId = "beispiel-1",
            });
            s.Colonists.Add(new KarteiPawn
            {
                Name = "Möhrchen", NameFirst = "Mira", NameNick = "Möhrchen", NameLast = "Kranich",
                Title = "Ärztin", GenderLabel = "Weiblich", AgeBiological = 38, AgeChronological = 38, Xenotype = "Baseliner",
                StatusLabel = "in der Kolonie", Mood = 58,
                Childhood = new KarteiStory { Title = "Krankenhaushelferin", Description = "Mira verbrachte ihre Kindheit zwischen Betten und Verbänden." },
                Adulthood = new KarteiStory { Title = "Landärztin", Description = "Als einzige Ärztin weit und breit behandelte Mira alles von Grippe bis Schusswunden." },
                Traits = new List<string> { "Freundlich", "Schneller Lerner" },
                Skills = Skills(S("Schießen", 2), S("Nahkampf", 1), S("Bauen", 3), S("Bergbau", 1), S("Kochen", 5), S("Pflanzen", 4),
                    S("Tiere", 3), S("Handwerk", 4), S("Kunst", 2), S("Medizin", 12, "Leidenschaft"), S("Sozial", 8, "Interesse"), S("Intellektuell", 9, "Interesse")),
                Weapon = null, Apparel = new List<string> { "Kittel", "Hose aus Baumwolle" },
                Relations = new List<KarteiRelation> { new KarteiRelation { Label = "Ehemann", Other = "Spargelkönig" } },
                KillsHumanlike = 0, KillsAnimals = 1, DaysAsColonist = 37.2, ViewerLinked = true, LinkedUserId = "beispiel-2",
            });
            s.Colonists.Add(new KarteiPawn
            {
                Name = "Dulli", NameFirst = "Dieter", NameNick = "Dulli", NameLast = "Wendt",
                Title = "Tagelöhner", GenderLabel = "Männlich", AgeBiological = 27, AgeChronological = 27, Xenotype = "Baseliner",
                StatusLabel = "in der Kolonie", Mood = 44,
                Childhood = new KarteiStory { Title = "Straßenkind", Description = "Dieter lernte auf der Straße, schnell zu laufen." },
                Adulthood = null,
                Traits = new List<string> { "Weichei" },
                Skills = Skills(S("Schießen", 5), S("Nahkampf", 2), S("Bauen", 8, "Interesse"), S("Bergbau", 9, "Leidenschaft"), S("Kochen", 0, disabled: true),
                    S("Pflanzen", 3), S("Tiere", 1), S("Handwerk", 5), S("Kunst", 0, disabled: true), S("Medizin", 0), S("Sozial", 1), S("Intellektuell", 0)),
                Health = new List<KarteiCondition> { new KarteiCondition { Label = "Grippe", Part = null } },
                Weapon = "Revolver (normal)", Apparel = new List<string> { "Parka" },
                KillsHumanlike = 1, KillsAnimals = 0, DaysAsColonist = 9.8, ViewerLinked = false,
            });
            s.Colonists.Add(new KarteiPawn
            {
                Name = "Kanzlerin", NameFirst = "Klara", NameNick = "Kanzlerin", NameLast = "Bohne",
                Title = "Diplomatin", GenderLabel = "Weiblich", AgeBiological = 55, AgeChronological = 55, Xenotype = "Baseliner",
                StatusLabel = "mit einer Karawane unterwegs", Mood = 82,
                Childhood = new KarteiStory { Title = "Adelstochter", Description = "Klara wuchs mit Etikette und Tischmanieren auf." },
                Adulthood = new KarteiStory { Title = "Diplomatin", Description = "Klara verhandelte Frieden zwischen Siedlungen, die sich lieber beschossen hätten." },
                Traits = new List<string> { "Zäh" },
                Skills = Skills(S("Schießen", 3), S("Nahkampf", 2), S("Bauen", 1), S("Bergbau", 0), S("Kochen", 2), S("Pflanzen", 1),
                    S("Tiere", 4), S("Handwerk", 1), S("Kunst", 6, "Interesse"), S("Medizin", 2), S("Sozial", 15, "Leidenschaft"), S("Intellektuell", 7)),
                Weapon = "Messer (gut)", Apparel = new List<string> { "Staubmantel", "Hose aus Leder" },
                KillsHumanlike = 0, KillsAnimals = 2, DaysAsColonist = 21.5, ViewerLinked = true, LinkedUserId = "beispiel-3",
            });
            s.Deceased.Add(new KarteiPawn
            {
                Name = "Rübe", NameFirst = "Rudi", NameNick = "Rübe", NameLast = "Haller",
                Title = "Jäger", GenderLabel = "Männlich", AgeBiological = 33, AgeChronological = 33, Xenotype = "Baseliner",
                StatusLabel = "verstorben", Mood = null,
                Childhood = new KarteiStory { Title = "Waldkind", Description = "Rudi kannte jeden Pfad im Wald." },
                Adulthood = new KarteiStory { Title = "Jäger", Description = "Rudi versorgte sein Dorf mit Fleisch und Fellen." },
                Traits = new List<string> { "Nachteule" },
                Skills = Skills(S("Schießen", 10, "Leidenschaft"), S("Nahkampf", 4), S("Bauen", 2), S("Bergbau", 1), S("Kochen", 3), S("Pflanzen", 2),
                    S("Tiere", 8, "Interesse"), S("Handwerk", 1), S("Kunst", 0), S("Medizin", 1), S("Sozial", 2), S("Intellektuell", 0)),
                Weapon = null,
                KillsHumanlike = 6, KillsAnimals = 24, DaysAsColonist = 30.1, ViewerLinked = true, LinkedUserId = "beispiel-4", DiedLabel = null,
            });
            s.Colony = new KarteiColony
            {
                ColonyName = "Spargelhof", FactionName = "Freie Siedler vom Spargelhof", DateLabel = "7. Aprimay 5501", DaysPassed = 37,
                Storyteller = "Cassandra Classic", Difficulty = "Raue Welt", Biome = "Gemäßigter Wald",
                WealthTotal = 24850f, WealthItems = 9120f, WealthBuildings = 11230f, WealthPawns = 4500f,
                Colonists = s.Colonists.Count, Prisoners = 1, Animals = 5, Deceased = s.Deceased.Count, AverageMood = 64,
                ColonyKarma = 18f, ColonyKarmaLabel = "ausgeglichen",
                Mods = new List<string> { "Harmony", "[CAP] RimWorld Interactive Chat Services", "Twitch Companion for RimWorld" },
            };
            return s;
        }
    }
}
