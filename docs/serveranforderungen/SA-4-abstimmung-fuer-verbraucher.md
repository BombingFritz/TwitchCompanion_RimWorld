<!-- Entwurf, NICHT AUF GITHUB ANGELEGT: neues Issue im Server-Repository, Vorlage „Serveranforderung“. -->

**Titel:** Serveranforderung: Twitch-Abstimmung im Auftrag eines lokalen Verbrauchers starten und Ergebnis melden (RimWorld)

## Bedarf
Der Chat soll in RimWorld über Spielereignisse abstimmen (z. B. welches von drei Ereignissen als Nächstes kommt, oder wie ein Raid empfangen wird). Die Abstimmungen sollen echte Twitch-Abstimmungen sein, damit sie im eigenen Abstimm-Widget SpargelPoll erscheinen und die Ergebniszeile wie gewohnt vom Server kommt. Der Kanal ist Partner (Nutzer, 19.11.2026). Die RimWorld-Mod RICS hat keine eigenen Abstimmungen.

## Verbraucher und Stand
- Repository und eigenes Feature-Issue: `BombingFritz/TwitchCompanion_RimWorld`, Spec `docs/specs/mod.md` (Stufe 2 „Chat entscheidet“, Stufe 3); Feature-Issue noch nicht angelegt
- Getesteter Server-Stand (Commit oder Release): `main` @ `0428608` (nur gelesen)
- Betroffene Spec: `wetten-abstimmungen.md` (Stand der Abstimmung, Ergebniszeilen); `SCOPES` enthält heute nur `channel:read:polls`

## Aktuelles Verhalten
Der Server verfolgt Abstimmungen über EventSub und sendet Ergebniszeilen, kann aber selbst keine starten. Ein Verbraucher kann keine Abstimmung anstoßen, ohne Twitch direkt aufzurufen (ADR 0023 verbietet das).

## Benötigte Fähigkeit
- Ein lokaler Verbraucher übergibt Titel, 2 bis 5 Optionen und eine Dauer; der Server startet damit eine Twitch-Abstimmung und antwortet sofort, ob sie läuft (mit ID) oder warum nicht (schon eine Abstimmung offen, Berechtigung fehlt, Twitch-Fehler).
- Kanalpunkte-Stimmen je Abstimmung einschaltbar, mit Preis je Zusatzstimme, den der Verbraucher mitgibt. Die Mod schaltet bei etwa jeder dritten Abstimmung Zusatzstimmen für 3000 Kanalpunkte ein (Nutzer, 19.11.2026).
- Nach dem Ende meldet der Server dem Verbraucher das Ergebnis: Status, Stimmen je Option, Gewinner (bei Gleichstand alle Gleichplatzierten), auch bei abgebrochener Abstimmung.
- Grenzen von Twitch: Titel bis 60, Optionen bis 25 Zeichen, 15 bis 1800 s. Ob der Server kürzt oder ablehnt, entscheidet der Server; die Mod kürzt vorher selbst.
- Später ähnlich für Wetten aus Spielereignissen; eigenes Issue, wenn es so weit ist.

Optionaler API-Vorschlag (noch nicht vereinbart): `POST /api/polls/start` `{client, title, choices: [..], durationS, channelPointsPerVote}` → `{ok, pollId}` oder `{ok:false, reason}`; Ergebnis als Ereignis `poll_result` `{pollId, status, choices: [{title, votes}], winners}` an den startenden Verbraucher im Ereignisstrom aus Issue #74.

## Akzeptanzkriterien
- [ ] Start mit 3 Optionen und 60 s: Twitch-Abstimmung läuft, SpargelPoll zeigt sie, der Verbraucher erhält die ID.
- [ ] Mit `channelPointsPerVote: 3000`: Zusatzstimmen kosten 3000 Kanalpunkte; ohne Angabe keine Kanalpunkte-Stimmen.
- [ ] Nach dem Ende: genau eine Ergebnismeldung an den Verbraucher, Ergebniszeile im Chat wie bisher.
- [ ] Schon eine Abstimmung offen oder Berechtigung fehlt: sofortige Antwort mit Grund, nichts gestartet.
- [ ] Abstimmung in der Twitch-Oberfläche abgebrochen: Ergebnismeldung mit Status abgebrochen.
- [ ] Verhalten bei Fehlern oder Ausfall geklärt: Verbraucher nicht mehr verbunden → Abstimmung läuft zu Ende, Ergebnis geht verloren, kein Fehler.
- [ ] Rückwärtskompatibilität oder Migration geklärt: neuer Scope `channel:manage:polls`, einmal neu anmelden (`/login`); ohne Scope wie heute.

## Auswirkungen
- Blockiert „Chat entscheidet“ beim Raid (Stufe 2) und Stufe 3 der Mod.
- Unabhängige Weiterarbeit: Stufe 1 und der Raid mit reinem Zufall gehen ohne.
