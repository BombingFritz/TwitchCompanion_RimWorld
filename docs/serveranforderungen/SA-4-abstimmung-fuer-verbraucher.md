<!-- Entwurf für ein neues Issue im Server-Repository, Vorlage „Serveranforderung“. Noch nicht angelegt. -->

**Titel:** Serveranforderung: Twitch-Abstimmung im Auftrag eines lokalen Verbrauchers starten und Ergebnis melden (RimWorld)

## Bedarf
Die Abstimmungen aus RimWorld (Twitch Toolkit: „welches Ereignis kommt als Nächstes?“, Rolle eines Raids) sollen als echte Twitch-Abstimmung laufen, damit sie im eigenen Abstimm-Widget SpargelPoll erscheinen und die Ergebniszeile wie gewohnt vom Server kommt. Der Kanal ist Partner, Abstimmungen stehen zur Verfügung (Nutzer, 19.11.2026). Bisher stimmt der Chat im Toolkit mit Zahlen ab, ohne Widget.

## Verbraucher und Stand
- Repository und eigenes Feature-Issue: `BombingFritz/TwitchCompanion_RimWorld`, Spec `docs/specs/mod.md` (Stufe 2 „Chat entscheidet“ und Stufe 3)
- Getesteter Server-Stand (Commit oder Release): `main` @ `0428608` (nur gelesen)
- Betroffene Spec: `wetten-abstimmungen.md` (Stand der Abstimmung, Ergebniszeilen); `SCOPES` enthält heute nur `channel:read:polls`

## Aktuelles Verhalten
Der Server verfolgt Abstimmungen über EventSub und sendet Ergebniszeilen, kann aber selbst keine starten (kein `channel:manage:polls`). Ein Verbraucher kann keine Abstimmung anstoßen, ohne Twitch direkt aufzurufen (ADR 0023 verbietet das).

## Benötigte Fähigkeit
- Ein lokaler Verbraucher übergibt Titel, 2 bis 5 Optionen und eine Dauer; der Server startet damit eine Twitch-Abstimmung und antwortet sofort, ob sie läuft (mit ID) oder warum nicht (z. B. schon eine Abstimmung offen, Berechtigung fehlt, Twitch-Fehler).
- Nach dem Ende meldet der Server dem Verbraucher das Ergebnis: Status, Stimmen je Option, Gewinner (bei Gleichstand alle Gleichplatzierten), auch wenn die Abstimmung abgebrochen wurde.
- Grenzen von Twitch: Titel bis 60, Optionen bis 25 Zeichen, 15 bis 1800 s. Ob der Server kürzt oder ablehnt, entscheidet der Server; die Mod kürzt vorher selbst.
- Kanalpunkte-Stimmen je Abstimmung einschaltbar, mit Preis je Zusatzstimme, den der Verbraucher mitgibt. Hintergrund: Die Zusatzstimmen des Toolkits für Subs, VIPs und Mods entfallen bei Twitch-Abstimmungen; die Mod will den Preis von Abstimmung zu Abstimmung nach Nachfrage anheben und wieder senken (Entscheidung des Nutzers steht noch aus). Dafür braucht sie im Ergebnis auch die Kanalpunkte-Stimmen je Option.
- Später ähnlich für Wetten (Predictions) aus Spielereignissen; eigenes Issue, wenn es so weit ist.

Optionaler API-Vorschlag (noch nicht vereinbart): `POST /api/polls/start` `{client, title, choices: [..], durationS}` → `{ok, pollId}` oder `{ok:false, reason}`; Ergebnis als Ereignis `poll_result` `{pollId, status, choices: [{title, votes}], winners}` an den startenden Verbraucher im Ereignisstrom aus Issue #74.

## Akzeptanzkriterien
- [ ] Start mit 3 Optionen und 60 s: Twitch-Abstimmung läuft, SpargelPoll zeigt sie, die Mod erhält die ID.
- [ ] Nach dem Ende: genau eine Ergebnismeldung an den Verbraucher, Ergebniszeile im Chat wie bisher.
- [ ] Schon eine Abstimmung offen oder Berechtigung fehlt: sofortige Antwort mit Grund, nichts gestartet.
- [ ] Abstimmung in der Twitch-Oberfläche abgebrochen: Ergebnismeldung mit Status abgebrochen.
- [ ] Verhalten bei Fehlern oder Ausfall geklärt: Verbraucher nicht mehr verbunden → Abstimmung läuft zu Ende, Ergebnis geht verloren, kein Fehler.
- [ ] Rückwärtskompatibilität oder Migration geklärt: neuer Scope, einmal neu anmelden (`/login`); ohne Scope wie heute.

## Auswirkungen
- Blockiert „Chat entscheidet“ beim Raid (Stufe 2) und Stufe 3 der Mod. Ohne diese Fähigkeit stimmt der Chat im Toolkit weiter mit Zahlen ab.
- Unabhängige Weiterarbeit: Stufe 1 und der Raid mit reinem Zufall gehen ohne.
