# Spec: Stream-Kartei für RimWorld (Vorschlag der Mod)

**Status:** Entwurf, Vorarbeit (Nutzer, 24.11.2026). Feature-Issue #36, Serveranforderung BombingFritz/Twitch-Companion-JS-server#98. Gegenstück: Server-Spec [`stream-kartei.md`](https://github.com/BombingFritz/Twitch-Companion-JS-server/blob/bd5e720/docs/specs/stream-kartei.md), Abschnitt „Stufe 2: RimWorld“ (Server `main` @ `bd5e720`, nur gelesen). Dort: „Die Mod liefert ihre Pawn-Liste lokal an den Server, etwa `POST /api/kartei/rimworld` mit eigenem Format. Der Server veröffentlicht sie mit demselben Modul … Inhalt und Felder entscheiden Mod und Nutzer; dafür legt die Mod eine Serveranforderung an.“ Die Website ist `BombingFritz/StreamKartei`.

Dieses Dokument ist der Vorschlag der Mod für Inhalt und Format. Verbindlich wird es erst mit dem Merge im Server.

## Ziel
Wie für UBOAT soll es für RimWorld eine eigene Seite der Stream-Kartei geben (Nutzer, 24.11.2026):
- **Personalakten** der Kolonisten, ausführlich, dazu die **Verstorbenen** (Nutzer, 24.11.2026: „Verstorbene“, auf der Seite eingeleitet mit „Wir gedenken: …“).
- **Statistik der ganzen Kolonie** als **eigene Unterseite** (Nutzer, 24.11.2026).
- Aktualisierung regelmäßig, aber nicht zu oft, damit Spiel und Server nicht leiden.

## Aufbau
```
RimWorld-Mod ──baut alle 10 min (nur bei Änderung)──> Stand (JSON)
      │
      └─POST lokal (Vorschlag: /api/kartei/rimworld)──> Server ──GitHub-API──> StreamKartei, Zweig „data“:
                                                          rimworld/pawns.json, rimworld/colony.json, Eintrag in index.json
```
- Der Server liest den RimWorld-Spielstand nicht selbst (anders als bei UBOAT): Spielstände sind groß, und die Mod hat die Daten im Spiel ohnehin, samt deutscher Bezeichnungen und der Zuordnung zu Zuschauern.
- Die Mod baut den Stand im Hauptthread des Spiels, höchstens alle 10 min (`karteiIntervalMinutes`), und gibt ihn nur weiter, wenn sich der Inhalt geändert hat (Fingerabdruck ohne Zeitstempel). Der Server hält zusätzlich seinen Mindestabstand von 10 min zwischen Veröffentlichungen.

## Datenschutz
- Twitch-Logins und -Namen erscheinen nicht auf der Website (Server-Spec, Entscheidung vom 21.11.2026).
- Die Mod schickt dem Server je verknüpfter Figur die Twitch-ID (`linkedUserId`), **nur lokal**: Damit kann der Server Figuren von `!privat`-Zuschauern ausblenden, auch wenn jemand `!privat` erst später einschaltet. Der Server entfernt das Feld vor dem Veröffentlichen. Die Vorschau-Datei der Mod enthält es nie.
- Die Mod filtert selbst, soweit sie `!privat` in diesem Lauf gesehen hat; maßgeblich ist der Filter des Servers mit seiner vollständigen Liste.
- **Name der Figur** (Nutzer, 24.11.2026): Der Spitzname ist der Hauptname, den das Spiel überall zeigt, und passt zu den Zuschauern; er wird benutzt (`name`). Bei Figuren von Zuschauern ist er bei RICS gleich dem Twitch-Namen. Twitch-Logins als eigenes Feld oder Zuordnungen gehen trotzdem nicht auf die Seite.
- Nicht enthalten: Raider, Killerkaninchen und andere Figuren außerhalb der Kolonie; Karma einzelner Zuschauer; Dateiname oder Ordner des Spielstands.

## Format (Vorschlag)
Alle Zeitpunkte ISO, Zahlen ohne Einheit. Texte kommen aus dem Spiel (in der Spielsprache, beim Nutzer Deutsch) und sind für die Website nicht vertrauenswürdig. Unbekannte Felder ignorieren; `schemaVersion` steigt nur bei nicht rückwärtskompatiblen Änderungen. Ein Wert, den das Spiel nicht hergibt, ist `null` bzw. eine leere Liste.

### `rimworld/pawns.json` – Personalakten
```json
{
  "schemaVersion": 1, "game": "rimworld", "gameVersion": "1.6.4871 rev591", "generatedAt": "…",
  "colonists": [ {
    "name": "Kiwi", "nameFirst": "Karla", "nameNick": "Kiwi", "nameLast": "Weber",
    "title": "Gärtnerin", "genderLabel": "Weiblich", "ageBiological": 31, "ageChronological": 95, "xenotype": "Baseliner",
    "statusLabel": "in der Kolonie", "mood": 64,
    "childhood": { "title": "Bauernkind", "description": "…" }, "adulthood": null,
    "traits": ["Fleißig"],
    "skills": [ { "name": "Pflanzen", "level": 9, "passion": "Leidenschaft", "disabled": false } ],
    "health": [ { "label": "Narbe", "part": "Linker Arm" } ],
    "weapon": "Revolver", "apparel": ["Hemd"],
    "relations": [ { "label": "Schwester", "other": "Rolf" } ],
    "killsHumanlike": 2, "killsAnimals": 5, "daysAsColonist": 12.3,
    "viewerLinked": true, "diedLabel": null
  } ],
  "deceased": [ { "name": "Rolf", "statusLabel": "verstorben", "diedLabel": null, "…": "…" } ]
}
```
- `colonists`: freie Kolonisten auf Karten, in Karawanen und unterwegs; sortiert nach Name.
- `deceased`: Verstorbene der Spielerfraktion, die das Spiel noch kennt. Auf der Seite unter „Wir gedenken: …“ (Nutzer, 24.11.2026). `diedLabel` (Spieldatum des Todes) [UNGEKLÄRT: Quelle im Spiel noch zu klären, vorerst `null`].
- `title`: Titel aus dem Lebenslauf; `childhood`/`adulthood`: Abschnitte des Lebenslaufs mit Beschreibung.
- `skills.passion`: „keine“, „Interesse“, „Leidenschaft“; `disabled`: Fertigkeit ist für die Figur gesperrt.
- `health`: nur sichtbare Einträge (Verletzungen, Krankheiten, Implantate), mit Körperteil.
- `mood`: Stimmung in Prozent; `null` bei Verstorbenen.
- `relations`: nur zu anderen Menschen der Kolonie, wichtigste Beziehung je Person.
- `viewerLinked`: Die Figur gehört einem Zuschauer, ohne zu sagen, welchem.
- An den Server zusätzlich `linkedUserId` (Twitch-ID, nur lokal, siehe oben).

### `rimworld/colony.json` – Statistik der Kolonie (Unterseite)
```json
{
  "schemaVersion": 1, "game": "rimworld", "gameVersion": "…", "generatedAt": "…",
  "colonyName": "Spargelhof", "factionName": "Neue Siedler", "dateLabel": "7. Aprimay 5501", "daysPassed": 37,
  "storyteller": "Cassandra", "difficulty": "Raue Welt", "biome": "Gemäßigter Wald",
  "wealth": { "total": 13166, "items": 6000, "buildings": 5000, "pawns": 2166 },
  "colonists": 6, "prisoners": 0, "animals": 3, "deceased": 1, "averageMood": 64,
  "colonyKarma": { "value": 13, "label": "ausgeglichen" },
  "mods": ["RICS", "Twitch Companion"]
}
```
- Werte der Heimatkarte mit den meisten Kolonisten; `colonyKarma` aus dem Karma-System der Mod (Spec `mod.md`, Abschnitt „Karma“), `null`, wenn es aus ist.
- `mods`: aktive Mods ohne die offiziellen Inhalte (wie `mods` bei UBOAT).
- Später denkbar [nicht umgesetzt]: Verlauf über die Zeit (Wohlstand, Kolonisten), Zahl der überstandenen Raids, gewählte Ereignisse.

### Beispieldaten (Dummys)
Erfundene Daten im Format liegen unter `docs/kartei-beispiel/` (Personalakten, Statistik, Vorschlag für den Körper an den Server). Sie dienen den Vorarbeiten für Website und Server, solange der Vertrag fehlt; die Mod erzeugt sie selbst, ein Test hält sie aktuell.

### Größe
Bei 10 bis 20 Kolonisten etwa 30 bis 60 KB für `pawns.json`, unter 2 KB für `colony.json`.

## Vorarbeit in der Mod (umgesetzt)
- `Core/Kartei.cs`: Datenmodell, JSON (`KarteiJson.Pawns`, `KarteiJson.Colony`), Fingerabdruck, Takt. Unit-Tests `KarteiTests`.
- `KarteiExport.cs`: baut den Stand aus dem Spiel (Kolonisten, Verstorbene, Kolonie, Zuordnung zu Zuschauern aus RICS).
- Einstellung „Stream-Kartei“: Vorschau-Datei regelmäßig schreiben (Vorgabe aus), Takt in Minuten, Knopf „Vorschau jetzt schreiben“. Die Vorschau liegt im RimWorld-Konfigurationsordner unter `TwitchCompanion/kartei-rimworld-pawns.json` und `…-colony.json`, ohne Twitch-IDs.
- **Senden an den Server: noch nicht.** Erst nach dem Merge des Server-Vertrags (Serveranforderung), in einem eigenen PR. Der Server wird vorher umfangreich gewartet und umgebaut (Nutzer, 24.11.2026); bis dahin nur Vorarbeiten mit Dummys, am Server nur Issues.

## Entwurf: Senden (Branch `entwurf/kartei-senden`, nicht mergen vor dem Server-Vertrag)
- Schalter „ENTWURF: an den Server senden“ (Vorgabe aus, nicht im Stream): Die Mod schickt den Stand an den **vorgeschlagenen** Endpunkt `POST /api/kartei/rimworld` (Körper wie `docs/kartei-beispiel/an-den-server.json`). Nicht angenommen → beim nächsten Takt erneut. Der heutige Server antwortet mit 404 („kennt den Endpunkt noch nicht“).
- Attrappe `tools/attrappe-server.mjs`: nimmt den Stand an, prüft die Grundform, `GET /attrappe/kartei` zeigt ihn so, wie er veröffentlicht würde (ohne `linkedUserId`).
- Nach dem Merge des Server-Vertrags: an den vereinbarten Endpunkt und das vereinbarte Antwortformat anpassen, eigener PR.

## Offen
- [UNGEKLÄRT] Todesdatum der Verstorbenen.
- Gestaltung der Seiten liegt bei `BombingFritz/StreamKartei`; ihr Format kommt aus dem Server-Vertrag.
