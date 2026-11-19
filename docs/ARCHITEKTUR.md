# Architektur (Ist-Zustand)

Noch kein Code. Geplanter Aufbau: [Spec](specs/mod.md), Abschnitt „Aufbau“; Entscheidungen in [adr/](adr/).

## Serverstand und Integration
Führender Ort für den Stand der Anbindung an den Server `BombingFritz/Twitch-Companion-JS-server` (Regeln in `AGENTS.md`). Nur gemergte, auf `main` des Servers nachgewiesene Fähigkeiten zählen als verfügbar.

**Zuletzt geprüfter Serverstand:** `main` bei Commit `0428608` (19.11.2026), nur auf GitHub gelesen. Welcher Stand auf dem Streaming-PC installiert ist, ist hier nicht nachgewiesen.

**Genutzte Verträge:** keine. Alles, was die Mod braucht, ist noch nicht vereinbart.

**Benötigte Fähigkeiten des Servers:**
| Fähigkeit | Entwurf | Server-Issue | Blockiert |
|---|---|---|---|
| Chat-Befehle nach Wort und Präfix empfangen, Chatzeilen über die Absender-Logik senden | SA-1 | Ergänzung zu #74, nicht angelegt | Stufe 1 |
| Liste aktiver Chatter und aktuelle Kategorie | SA-2 | nicht angelegt | Coins und Kategorieprüfung in Stufe 1 |
| Eingehende Raids melden | SA-3 | nicht angelegt | Stufe 2 |
| Twitch-Abstimmung starten und Ergebnis melden | SA-4 | nicht angelegt | „Chat entscheidet“ in Stufe 2, Stufe 3 |

**Legacy-Ausnahmen:** keine (die Mod ruft Twitch nie direkt auf).

## RICS-Stand
Geprüft am Quellcode, Commit vom 22.10.2026 (nach Release 1.54), nicht im Spiel. Genutzte Einstiegspunkte: Spec, Abschnitt „Einstiegspunkte und Patches“.
