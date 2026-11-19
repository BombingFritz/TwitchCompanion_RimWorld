# Architektur (Ist-Zustand)

Noch kein Code. Geplanter Aufbau: [Spec](specs/mod.md), Abschnitt „Aufbau“; Entscheidungen in [adr/](adr/).

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
Geprüft am Quellcode, Commit vom 22.10.2026 (nach Release 1.54), nicht im Spiel. Genutzte Einstiegspunkte: Spec, Abschnitt „Einstiegspunkte und Patches“.
