# Ereignisse für die Abstimmung

Diese Ereignisse darf der Erzähler zur Abstimmung stellen (Stufe 3, Spec `specs/mod.md`). Die Mod nimmt die Liste aus `Source/TwitchCompanion/Core/EventCatalog.cs`; beide werden zusammen geändert.

Vorgaben (Nutzer, 20.11.2026): nichts Verheerendes, nichts extrem Gutes. Gestrichen vom Nutzer (20.11.2026): Muskelparasiten, Schlafkrankheit, faserige und sensorische Mechaniten. Ausgleich über ein Gesamt-Karma ist für später geplant. Einordnung und Beschreibung stammen von Claude, Vorlage war die Ereignisliste von RICS. Ereignisse eines nicht installierten DLC fallen von selbst weg. Vor jeder Runde prüft das Spiel, ob ein Ereignis gerade möglich ist.

Zusätzlich hat jede Runde eine ruhige Option: „Nichts passiert“ oder „Wetterwechsel“ (nächstes Wetter, wie es das Spiel auch sonst wählen würde). Das Wetter wechselt außerdem normal weiter, wie im Spiel vorgesehen.

**Zum Streichen:** Zeilen, die nicht kommen sollen, aus „Drin“ löschen. Soll etwas aus „Draußen“ doch dazu, die Zeile nach oben verschieben.

## Drin

### Gut
| Ereignis (Spielname) | Beschreibung |
|---|---|
| `WandererJoin` | Ein Wanderer schließt sich der Kolonie an (gut, nicht extrem gut; Nutzer, 20.11.2026) |
| `RefugeePodCrash` | Transportkapsel stürzt ab, Insasse kann aufgenommen werden |
| `ResourcePodCrash` | Frachtkapseln mit Gütern landen |
| `FarmAnimalsWanderIn` | Nutztiere laufen zu |
| `SelfTame` | Ein wildes Tier wird von selbst zahm |
| `AmbrosiaSprout` | Ambrosia wächst |
| `PsychicSoothe` | Psychische Beruhigung, bessere Stimmung |
| `Aurora` | Polarlicht, bessere Stimmung |
| `RefugeePodCrash_Baby` | Transportkapsel mit einem Baby stürzt ab (Biotech; Nutzer, 20.11.2026) |

### Neutral
| Ereignis (Spielname) | Beschreibung |
|---|---|
| `TraderCaravanArrival` | Handelskarawane kommt |
| `OrbitalTraderArrival` | Handelsschiff im Orbit |
| `VisitorGroup` | Besuch kommt vorbei |
| `TravelerGroup` | Reisende ziehen durch |
| `HerdMigration` | Tierherde zieht durch |
| `ThrumboPasses` | Thrumbos ziehen vorbei |
| `WildManWandersIn` | Wilder Mensch taucht auf |
| `MeteoriteImpact` | Meteorit schlägt ein |
| `ShipChunkDrop` | Schiffstrümmer fallen |
| `CaravanArrivalTributeCollector` | Tributeintreiber des Imperiums (Royalty) |

### Schlecht
| Ereignis (Spielname) | Beschreibung |
|---|---|
| `AnimalInsanitySingle` | Ein Tier dreht durch |
| `Alphabeavers` | Alphabiber fressen Bäume |
| `CropBlight` | Pflanzenfäule auf den Feldern |
| `ShortCircuit` | Kurzschluss |
| `SolarFlare` | Sonneneruption, Strom fällt aus |
| `Eclipse` | Sonnenfinsternis |
| `Flashstorm` | Blitzgewitter |
| `ColdSnap` | Kälteeinbruch |
| `HeatWave` | Hitzewelle |
| `CaravanDemand` | Zahlungsforderung |
| `Disease_Flu` | Grippe |
| `Disease_Malaria` | Malaria |
| `Disease_Plague` | Pest |
| `Disease_GutWorms` | Darmwürmer |
| `Disease_AnimalFlu` | Tiergrippe |
| `Disease_AnimalPlague` | Tierpest |
| `NoxiousHaze` | Saurer Smog (Biotech) |

## Draußen (Vorschlag)

Verheerendes und extrem Gutes aus dieser Liste kommt seit dem Karma-System (Spec, Abschnitt „Karma“) über die ausgleichende Gerechtigkeit doch vor: Verheerendes, wenn das Karma der Kolonie sehr gut ist, extrem Gutes, wenn es sehr schlecht ist. Welche, steht in `EventCatalog.Doom` und `EventCatalog.Blessing`.

| Ereignis (Spielname) | Warum draußen |
|---|---|
| `RaidEnemy`, `RaidFriendly` | Raids kommen vom Spiel-Erzähler und später als Twitch-Raid mit Zuschauernamen (Stufe 2); RICS führt sie nicht in der Ereignisliste |
| `Ambush`, `ManhunterAmbush`, `ManhunterPack`, `AnimalInsanityMass` | verheerend |
| `Infestation`, `Infestation_Jelly`, `WastepackInfestation` | verheerend, laut RICS |
| `DefoliatorShipPartCrash`, `PsychicEmanatorShipPartCrash`, `MechCluster`, `ProblemCauser` | verheerend |
| `PsychicDrone`, `ToxicFallout`, `VolcanicWinter` | verheerend, laut RICS |
| `Disease_OrganDecay` | sehr schwer (Organe fallen aus) |
| `Disease_Abasia`, `Disease_BloodRot`, `WandererJoinAbasia` | Royalty-Krankheiten, schwer |
| `GiveQuest_*` (Quests, Bettler, Pilger, Arbeitsstätte, Komplexe) | kein Ereignis, sondern ein Auftrag; teils sehr lohnend |
| `AnimaTreeSpawn`, `GauranlenPodSpawn`, `PoluxTreeSpawn`, `WanderersSkylantern` | extrem gut bzw. besonders (Royalty, Ideology, Biotech) |
| `StrangerInBlackJoin`, `GameEndedWanderersJoin` | nur für eine Kolonie ohne Siedler gedacht |
| `CaravanMeeting`, `RansomDemand`, Endspiel-Quests | passen nicht zu einer Abstimmung |
| Anomaly (z. B. Bluttregen, Fleischbestien, Monolith) | großenteils verheerend; nur falls installiert und gewünscht |
| Odyssey (z. B. Dürre, Vulkanasche, Lava, Überflutung) | nur falls installiert und gewünscht |
| Wetter-Ereignisse | Wetter wechselt normal wie im Spiel; dazu die ruhige Option „Wetterwechsel“ |
