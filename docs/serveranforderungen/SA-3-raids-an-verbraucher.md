<!-- Entwurf für ein neues Issue im Server-Repository, Vorlage „Serveranforderung“. Noch nicht angelegt. -->

**Titel:** Serveranforderung: Eingehende Raids an lokale Verbraucher melden (RimWorld: Raid im Spiel)

## Bedarf
Ein Twitch-Raid soll in RimWorld als Gruppe ankommen: Der Anführer heißt wie der raidende Kanal, Zuschauer tragen sich als Mitglieder ein. Ob die Gruppe angreift, hilft, handelt oder nur besucht, entscheidet die Mod per Zufall, auf Wunsch per Abstimmung (Nutzer, 19.11.2026). Dafür muss die Mod erfahren, dass ein Raid eingeht. Die Fähigkeit ist allgemein und könnte auch anderen Spielen oder Widgets dienen.

## Verbraucher und Stand
- Repository und eigenes Feature-Issue: `BombingFritz/TwitchCompanion_RimWorld`, Spec `docs/specs/mod.md` (Stufe 2)
- Getesteter Server-Stand (Commit oder Release): `main` @ `0428608` (nur gelesen)
- Betroffene Spec: `raid-begruessung.md` (bleibt unverändert, der Raid soll zusätzlich gemeldet werden)

## Aktuelles Verhalten
Der Server empfängt `channel.raid`, begrüßt und zählt den Raid. Verbraucher erfahren davon nichts. Früher hat die Mod ToolkitRaids die Raid-Meldung aus dem IRC von ToolkitCore gelesen; diese Verbindung gibt es mit dem Server nicht mehr.

## Benötigte Fähigkeit
- Je eingehendem Raid eine Meldung an verbundene Verbraucher, binnen weniger Sekunden: eindeutige ID, Zeitpunkt, raidender Kanal (ID, Login, Anzeigename), Zuschauerzahl.
- Jeder Raid genau einmal, auch bei doppelt zugestellten Ereignissen (dort ADR 0009). Ausgehende Raids nicht.
- Kein Nachliefern, wenn die Mod gerade nicht verbunden ist.
- Rolle, Gruppengröße und Beitritt regelt die Mod; der Beitrittsbefehl läuft über den Chat-Kanal aus Issue #74.

Optionaler API-Vorschlag (noch nicht vereinbart): Ereignis `raid` `{id, at, fromId, fromLogin, fromName, viewers}` im Ereignisstrom aus Issue #74, für Verbraucher, die es anfordern (z. B. `events=raid`).

## Akzeptanzkriterien
- [ ] Eingehender Raid mit 12 Zuschauern: genau eine Meldung mit Name und Zahl.
- [ ] Doppelt zugestellte Raid-Notification: eine Meldung.
- [ ] Ausgehender Raid: keine Meldung.
- [ ] Raid-Begrüßung und Statistik unverändert.
- [ ] Verhalten bei Fehlern oder Ausfall geklärt: ohne verbundenen Verbraucher nichts, kein Fehler.
- [ ] Rückwärtskompatibilität: nur ein neues Ereignis.

## Auswirkungen
- Blockiert Stufe 2 der Mod (Raid im Spiel). Stufe 1 ist unabhängig.
- Demo möglich: Die Mod kann den Raid im Spiel auch über ein Entwicklermenü auslösen.
