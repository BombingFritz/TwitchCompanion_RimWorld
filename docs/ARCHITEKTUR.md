# Architektur (Ist-Zustand)

Version 0.1 im Aufbau (Feature-Issue #2), noch ohne Serveranbindung. Geplanter Gesamtaufbau: [Spec](specs/mod.md), Abschnitt „Aufbau“; Entscheidungen in [adr/](adr/).

| Datei | Inhalt |
|---|---|
| `About/About.xml`, `LoadFolders.xml` | Mod-Daten; packageId `BombingFritz.TwitchCompanion.RimWorld`, braucht Harmony und RICS (`Captolamia.RICS`), lädt danach |
| `Source/TwitchCompanion/Core/` | reine Logik ohne RimWorld: `ChatLine` (Chatnachricht), `LineMerger` (Zeilen zusammenfassen, thread-sicher), `PrivacyNotices` (Hinweis an `!privat`-Zuschauer einmal je Sitzung) |
| `Source/TwitchCompanion/Rics/RicsApi.cs` | findet die RICS-Stellen per Reflexion, gibt Nachrichten an RICS weiter (E1) |
| `Source/TwitchCompanion/Rics/RicsPatches.cs` | Harmony-Patches P1 bis P3 |
| `Source/TwitchCompanion/Bridge.cs` | Eingangs-Warteschlange, Ausgang, Testmodus |
| `Source/TwitchCompanion/TwitchCompanionMod.cs` | Mod-Einstellungen (Brücke an/aus, Server-Adresse, Testmodus mit Testnachricht), Start, `GameComponent` als Hauptthread-Takt |
| `Source/TwitchCompanion.Tests/` | Unit-Tests für `Core/` |

Ablauf: Eingang → `Bridge.EnqueueIncoming` (beliebiger Thread) → je Bild bis zu 20 Nachrichten im Hauptthread an RICS. RICS-Antworten → Patches → `LineMerger` → nach 1 s Ruhe als zusammengefasste Nachricht hinaus (heute nur ins Log).

## Serverstand und Integration
Führender Ort für den Stand der Anbindung an den Server `BombingFritz/Twitch-Companion-JS-server` (Regeln in `AGENTS.md`). Nur gemergte, auf `main` des Servers nachgewiesene Fähigkeiten zählen als verfügbar.

**Zuletzt geprüfter Serverstand:** `main` bei Commit `0428608` (19.11.2026), nur auf GitHub gelesen. Welcher Stand auf dem Streaming-PC installiert ist, ist hier nicht nachgewiesen.

**Genutzte Verträge:** keine. Alles, was die Mod braucht, ist noch nicht vereinbart.

**Benötigte Fähigkeiten des Servers:**
| Fähigkeit | Kurzname | Server-Issue | Blockiert |
|---|---|---|---|
| Chat-Befehle nach Wort und Präfix empfangen, Chatzeilen über die Absender-Logik senden | SA-1 | [#74](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/74) mit [Ergänzung](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/74#issuecomment-7024624436) | Stufe 1 (#2) |
| Liste aktiver Chatter und aktuelle Kategorie | SA-2 | [#79](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/79) | Coins und Kategorieprüfung in Stufe 1 (#2), Raid-Beitritt (#3) |
| Eingehende Raids melden | SA-3 | [#80](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/80) | Stufe 2 (#3) |
| Twitch-Abstimmung starten und Ergebnis melden | SA-4 | [#81](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/81) | „Chat entscheidet“ in Stufe 2 (#3), Stufe 3 (#4) |

**Legacy-Ausnahmen:** keine (die Mod ruft Twitch nie direkt auf).

## RICS-Stand
RICS wird auf den Branches `1.6` und `1.7` entwickelt, nicht auf `master`. Geprüft am Quellcode von `1.6` bei Commit `b825e25` (17.11.2026, Version 1.57): Alle Einstiegspunkte aus der Spec sind vorhanden. Installiert auf dem Streaming-PC: 1.56 (laut `Player.log` vom 20.11.2026). Spielstand lädt mit RICS ohne Fehler.
