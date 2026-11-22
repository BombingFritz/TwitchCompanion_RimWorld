// SPDX-License-Identifier: AGPL-3.0-or-later
// Twitch Companion for RimWorld – welche Ereignisse der Erzähler zur Abstimmung stellen darf (Liste: docs/ereignisse.md).

using System.Collections.Generic;
using System.Linq;

namespace TwitchCompanion.RimWorld.Core
{
    public sealed class CatalogEntry
    {
        public readonly string DefName;
        public readonly EventKarma Karma;
        public readonly string Beschreibung;
        public CatalogEntry(string defName, EventKarma karma, string beschreibung) { DefName = defName; Karma = karma; Beschreibung = beschreibung; }
    }

    /// <summary>
    /// Ereignisse für die Abstimmung (Nutzer, 20.11.2026): nichts Verheerendes und nichts extrem Gutes; das soll später
    /// ein Gesamt-Karma ausgleichen. Einordnung von Claude, Vorlage: Ereignisliste von RICS. Der Nutzer streicht in
    /// <c>docs/ereignisse.md</c>; diese Liste folgt ihr. Ereignisse fehlender DLCs fallen von selbst weg.
    /// </summary>
    public static class EventCatalog
    {
        public static readonly IReadOnlyList<CatalogEntry> Default = new[]
        {
            // Gut
            new CatalogEntry("WandererJoin", EventKarma.Good, "Ein Wanderer schließt sich der Kolonie an"),
            new CatalogEntry("RefugeePodCrash", EventKarma.Good, "Transportkapsel stürzt ab, Insasse kann aufgenommen werden"),
            new CatalogEntry("ResourcePodCrash", EventKarma.Good, "Frachtkapseln mit Gütern landen"),
            new CatalogEntry("FarmAnimalsWanderIn", EventKarma.Good, "Nutztiere laufen zu"),
            new CatalogEntry("SelfTame", EventKarma.Good, "Ein wildes Tier wird von selbst zahm"),
            new CatalogEntry("AmbrosiaSprout", EventKarma.Good, "Ambrosia wächst"),
            new CatalogEntry("PsychicSoothe", EventKarma.Good, "Psychische Beruhigung, bessere Stimmung"),
            new CatalogEntry("Aurora", EventKarma.Good, "Polarlicht, bessere Stimmung"),
            new CatalogEntry("RefugeePodCrash_Baby", EventKarma.Good, "Transportkapsel mit einem Baby stürzt ab (Biotech)"),
            // Neutral
            new CatalogEntry("TraderCaravanArrival", EventKarma.Neutral, "Handelskarawane kommt"),
            new CatalogEntry("OrbitalTraderArrival", EventKarma.Neutral, "Handelsschiff im Orbit"),
            new CatalogEntry("VisitorGroup", EventKarma.Neutral, "Besuch kommt vorbei"),
            new CatalogEntry("TravelerGroup", EventKarma.Neutral, "Reisende ziehen durch"),
            new CatalogEntry("HerdMigration", EventKarma.Neutral, "Tierherde zieht durch"),
            new CatalogEntry("ThrumboPasses", EventKarma.Neutral, "Thrumbos ziehen vorbei"),
            new CatalogEntry("WildManWandersIn", EventKarma.Neutral, "Wilder Mensch taucht auf"),
            new CatalogEntry("MeteoriteImpact", EventKarma.Neutral, "Meteorit schlägt ein"),
            new CatalogEntry("ShipChunkDrop", EventKarma.Neutral, "Schiffstrümmer fallen"),
            new CatalogEntry("CaravanArrivalTributeCollector", EventKarma.Neutral, "Tributeintreiber des Imperiums (Royalty)"),
            // Schlecht
            new CatalogEntry("AnimalInsanitySingle", EventKarma.Bad, "Ein Tier dreht durch"),
            new CatalogEntry("Alphabeavers", EventKarma.Bad, "Alphabiber fressen Bäume"),
            new CatalogEntry("CropBlight", EventKarma.Bad, "Pflanzenfäule auf den Feldern"),
            new CatalogEntry("ShortCircuit", EventKarma.Bad, "Kurzschluss"),
            new CatalogEntry("SolarFlare", EventKarma.Bad, "Sonneneruption, Strom fällt aus"),
            new CatalogEntry("Eclipse", EventKarma.Bad, "Sonnenfinsternis"),
            new CatalogEntry("Flashstorm", EventKarma.Bad, "Blitzgewitter"),
            new CatalogEntry("ColdSnap", EventKarma.Bad, "Kälteeinbruch"),
            new CatalogEntry("HeatWave", EventKarma.Bad, "Hitzewelle"),
            new CatalogEntry("CaravanDemand", EventKarma.Bad, "Zahlungsforderung"),
            new CatalogEntry("Disease_Flu", EventKarma.Bad, "Grippe"),
            new CatalogEntry("Disease_Malaria", EventKarma.Bad, "Malaria"),
            new CatalogEntry("Disease_Plague", EventKarma.Bad, "Pest"),
            new CatalogEntry("Disease_GutWorms", EventKarma.Bad, "Darmwürmer"),
            new CatalogEntry("Disease_AnimalFlu", EventKarma.Bad, "Tiergrippe"),
            new CatalogEntry("Disease_AnimalPlague", EventKarma.Bad, "Tierpest"),
            new CatalogEntry("NoxiousHaze", EventKarma.Bad, "Saurer Smog (Biotech)"),
        };

        /// <summary>Verheerend: nur über die ausgleichende Gerechtigkeit, wenn das Karma der Kolonie sehr gut ist.</summary>
        public static readonly IReadOnlyList<CatalogEntry> Doom = new[]
        {
            new CatalogEntry("ManhunterPack", EventKarma.Doom, "Rudel mordlustiger Tiere"),
            new CatalogEntry("Infestation", EventKarma.Doom, "Insektenbefall"),
            new CatalogEntry("AnimalInsanityMass", EventKarma.Doom, "Massenwahnsinn der Tiere"),
            new CatalogEntry("PsychicDrone", EventKarma.Doom, "Psychische Drohne"),
            new CatalogEntry("ToxicFallout", EventKarma.Doom, "Giftiger Niederschlag"),
            new CatalogEntry("VolcanicWinter", EventKarma.Doom, "Vulkanischer Winter"),
            new CatalogEntry("DefoliatorShipPartCrash", EventKarma.Doom, "Entlauber-Schiffsteil stürzt ab"),
            new CatalogEntry("PsychicEmanatorShipPartCrash", EventKarma.Doom, "Psycho-Emanator-Schiffsteil stürzt ab"),
        };

        /// <summary>Extrem gut: nur über die ausgleichende Gerechtigkeit, wenn das Karma der Kolonie sehr schlecht ist.</summary>
        public static readonly IReadOnlyList<CatalogEntry> Blessing = new[]
        {
            new CatalogEntry("AnimaTreeSpawn", EventKarma.Blessing, "Anima-Baum wächst (Royalty)"),
            new CatalogEntry("GauranlenPodSpawn", EventKarma.Blessing, "Gauranlen-Kapsel sprießt (Ideology)"),
            new CatalogEntry("PoluxTreeSpawn", EventKarma.Blessing, "Polux-Baum wächst (Biotech)"),
            new CatalogEntry("WanderersSkylantern", EventKarma.Blessing, "Wanderer mit Himmelslaterne (Ideology)"),
        };

        public static Dictionary<string, CatalogEntry> ByDefName(IEnumerable<CatalogEntry> entries) =>
            (entries ?? Default).GroupBy(e => e.DefName).ToDictionary(g => g.Key, g => g.First());
    }
}
