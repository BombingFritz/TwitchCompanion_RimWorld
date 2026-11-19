<!-- Entwurf, NICHT AUF GITHUB ANGELEGT: neues Issue im Server-Repository, Vorlage „Serveranforderung“. -->

**Titel:** Serveranforderung: Eingehende Raids an lokale Verbraucher melden (RimWorld: Raid im Spiel)

## Bedarf
Ein Twitch-Raid soll in RimWorld als Gruppe ankommen: Der Anführer heißt wie der raidende Kanal, Zuschauer treten bei. Die RimWorld-Mod RICS kann das schon, löst es aber nur über ihre eigene Twitch-Verbindung aus, die mit dem Server entfällt. Ob die Gruppe angreift, hilft, handelt oder besucht, entscheidet die RimWorld-Mod per Zufall, auf Wunsch per Abstimmung (Nutzer, 19.11.2026). Die Fähigkeit ist allgemein und könnte auch anderen Spielen oder Widgets dienen.

## Verbraucher und Stand
- Repository und eigenes Feature-Issue: `BombingFritz/TwitchCompanion_RimWorld`, Spec `docs/specs/mod.md` (Stufe 2); Feature-Issue noch nicht angelegt
- Getesteter Server-Stand (Commit oder Release): `main` @ `0428608` (nur gelesen)
- Betroffene Spec: `raid-begruessung.md` (bleibt unverändert, der Raid soll zusätzlich gemeldet werden)

## Aktuelles Verhalten
Der Server empfängt `channel.raid`, begrüßt und zählt den Raid. Verbraucher erfahren davon nichts.

## Benötigte Fähigkeit
- Je eingehendem Raid eine Meldung an verbundene Verbraucher, binnen weniger Sekunden: eindeutige ID, Zeitpunkt, raidender Kanal (ID, Login, Anzeigename), Zuschauerzahl.
- Jeder Raid genau einmal, auch bei doppelt zugestellten Ereignissen (ADR 0009). Ausgehende Raids nicht.
- Kein Nachliefern, wenn der Verbraucher gerade nicht verbunden ist.
- Rolle, Gruppengröße und Beitritt regelt die Mod; der Beitrittsbefehl läuft über den Chat-Kanal aus Issue #74.

Optionaler API-Vorschlag (noch nicht vereinbart): Ereignis `raid` `{id, at, fromId, fromLogin, fromName, viewers}` im Ereignisstrom aus Issue #74, für Verbraucher, die es anfordern.

## Akzeptanzkriterien
- [ ] Eingehender Raid mit 12 Zuschauern: genau eine Meldung mit Name und Zahl.
- [ ] Doppelt zugestellte Raid-Notification: eine Meldung.
- [ ] Ausgehender Raid: keine Meldung.
- [ ] Raid-Begrüßung und Statistik unverändert.
- [ ] Verhalten bei Fehlern oder Ausfall geklärt: ohne verbundenen Verbraucher nichts, kein Fehler.
- [ ] Rückwärtskompatibilität: nur ein neues Ereignis.

## Auswirkungen
- Blockiert Stufe 2 der Mod (Raid im Spiel). Stufe 1 ist unabhängig.
- Demo möglich: RICS hat laut Code eine Debug-Auslösung für den Twitch-Raid (`TwitchRaidDebug`).
