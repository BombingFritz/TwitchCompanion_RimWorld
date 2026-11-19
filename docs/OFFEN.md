# Offene Punkte

## Entschieden (Nutzer, 19.11.2026)
| # | Frage | Entscheidung |
|---|---|---|
| 1 | Grundlage | RICS statt Twitch Toolkit |
| 2 | Befehle und Coins nur in der Twitch-Kategorie „RimWorld“? | ja |
| 3 | Lurker ohne Coins? | ja, ohne Anreiz zum Spammen; dazu Preisinflation: mehr Ausgaben → höhere Preise, ohne Ausgaben sinken sie wieder |
| 4 | `!privat`-Zuschauer im Spiel? | vorerst ganz draußen, weil RICS über den Stream hinaus speichert |
| 5 | Zweiter Raid binnen 20 min | das Gegenteil des vorigen: feindlich ↔ verbündet; nach Händler oder Besuch normale Gewichtung |
| 6 | Gewichte der Raid-Rollen | 50/15/10/10/15 als Start |
| 7 | `!privat` im Chat-Kanal des Servers | so einfach und stabil wie möglich → Kennzeichen an jeder Nachricht |
| 8 | Zusatzstimmen mit Kanalpunkten | fest 3000 Punkte je Zusatzstimme, nur bei etwa jeder dritten Abstimmung |

## Noch zu entscheiden
| # | Frage | Vorschlag | Betrifft |
|---|---|---|---|
| 9 | „Etwa jede dritte Abstimmung“: Zufall 1/3 und auch für die Raid-Abstimmung? | ja, beides | Stufe 2/3 |
| 10 | Startwerte der Preisinflation: Halbwertszeit, Referenzumsatz, Obergrenze | 20 min, 3000 Coins, ×4 | 0.1b |
| 11 | `!privat` rückwirkend: Zuschauer in RICS entfernen, Kolonist umbenennen | ja | 0.1 |
| 12 | Befehl für den aktuellen Preisfaktor | `!preise` | 0.1b |
| 13 | Abstand der Ereignis-Abstimmungen und Anzahl der Optionen | 20 min, 3 Optionen | Stufe 3 |

## Zu prüfen (im Spiel bzw. im RICS-Code)
- RICS ohne Twitch-Token: **bestätigt** (`Player.log` vom 20.11.2026: RimWorld 1.6.4871, RICS 1.56, Spielstand mit Harmony, Ideology, Biotech und RICS geladen, keine Fehler, kein Verbindungsversuch zu Twitch). Der Hinweis „XML settings do not match the latest JSON backup“ war nach dem Umstieg zu erwarten; RICS hat danach eine neue Sicherung geschrieben, beim nächsten Start sollte er fehlen.
- RICS-Einstellung „Log All Chat Messages“ ist an: Dann stehen Chatnachrichten in der `Player.log`. Für Streams ausschalten, solange nicht zur Fehlersuche gebraucht (Datenschutz, `!privat`). „Enable Debug Logging“ kann zum Testen an bleiben.
- RICS führt das Spielereignis `RaidFriendly` (verbündete Gruppe, Vorgabe aus) – Kandidat für die Raid-Rolle „Verbündete“.
- Wie RICS Moderator, VIP und Subscriber aus einer Nachricht liest (`Viewer.UpdateFromMessage`).
- Nebenwirkungen, wenn `TwitchService.IsConnected` wahr meldet, ohne verbunden zu sein (Patch P2).
- Aktivität für Coins auffrischen, ohne die Nachrichtenzählung von RICS zu verfälschen; ob RICS ohne Kategorie RimWorld weiter Coins verteilt.
- Methode, an der RICS den Kaufpreis bestimmt (Inflation).
- Saubere Entfernung eines Zuschauers in RICS (`!privat` rückwirkend).
- Raid-Rollen Verbündete, Händler und Besuch; automatischer Beitritt aller, die im Fenster schreiben.
- Ob Twitch nur einen festen Kanalpunkte-Preis je Abstimmung kennt.
- Status des Steam-Workshop-Eintrags von RICS (laut einer Suchmaschinen-Kopie entfernt); GitHub-Releases als Quelle.

## Serveranforderungen und Feature-Issues
Angelegt am 19.11.2026; Stand und Abhängigkeiten in `ARCHITEKTUR.md`, Abschnitt „Serverstand und Integration“, und in den Feature-Issues #2 bis #5.
