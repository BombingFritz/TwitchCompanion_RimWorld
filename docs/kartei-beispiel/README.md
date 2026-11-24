# Beispieldaten Stream-Kartei RimWorld (Dummys)

Erfundene Daten im vorgeschlagenen Format (`docs/specs/kartei.md`). Alle Namen sind ausgedacht; kein Bezug zu echten Zuschauern. Für Vorarbeiten an Website und Server, solange der Vertrag fehlt.

| Datei | Inhalt |
|---|---|
| `rimworld/pawns.json` | Personalakten: 4 Kolonisten, 1 Verstorbener, so wie sie veröffentlicht würden (ohne Twitch-IDs) |
| `rimworld/colony.json` | Statistik der Kolonie für die Unterseite |
| `an-den-server.json` | Vorschlag für den Körper von `POST /api/kartei/rimworld`: beide Teile, Personalakten mit `linkedUserId` (hier erfunden: `beispiel-1` …), die der Server vor dem Veröffentlichen entfernt |

Die Dateien erzeugt die Mod selbst aus `Core/KarteiBeispiel.cs`; der Test `KarteiBeispielTests` prüft, dass sie aktuell sind. Neu erzeugen im Ordner `Source`:

```
KARTEI_BEISPIEL_SCHREIBEN=1 dotnet test
```
