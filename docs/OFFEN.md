# Offene Punkte

## Entschieden (Nutzer, 19.11.2026)
| # | Frage | Entscheidung |
|---|---|---|
| 1 | Befehle und Coins nur in der Twitch-Kategorie „RimWorld“? | ja |
| 2 | Lurker ohne Coins? | ja, ohne Anreiz zum Spammen (Coins je Zeitabschnitt, nicht je Nachricht); dazu **Preisinflation**: mehr Ausgaben → höhere Preise, ohne Ausgaben sinken sie wieder |
| 3 | `!privat`-Zuschauer im Spiel? | vorerst ganz draußen, weil das TTK über den Stream hinaus speichert |
| 4 | Zweiter Raid binnen 20 min | das Gegenteil des vorigen: feindlich ↔ verbündet; nach Händler oder Besuch normale Gewichtung |
| 5 | Gewichte der Raid-Rollen | 50/15/10/10/15 als Start |
| 6 | `!privat` im Chat-Kanal des Servers | so einfach und stabil wie möglich → Kennzeichen an jeder Nachricht |

## Noch zu entscheiden
| # | Frage | Vorschlag | Betrifft |
|---|---|---|---|
| 7 | Startwerte der Preisinflation: Halbwertszeit, Referenzumsatz, Obergrenze | 20 min, 3000 Coins, ×4 | 0.1b |
| 8 | Kanalpunkte-Stimmen: Twitch kann den Preis nicht innerhalb einer Abstimmung verdoppeln. A: Preis steigt von Abstimmung zu Abstimmung (in SpargelPoll sichtbar). B: Zusatzstimmen mit Coins im Chat, mit Verdopplung (nicht in SpargelPoll sichtbar) | A | Stufe 3, SA-4 |
| 9 | `!privat` rückwirkend: Zuschauerdaten im TTK löschen, Kolonist bekommt einen zufälligen Namen | ja | 0.1 |
| 10 | Beitrittsbefehl für Raider | `!dabei` | Stufe 2 |
| 11 | Befehl für den aktuellen Preisfaktor | `!preise` | 0.1b |

## Zu prüfen
- Ob TTK 1.6.0 mit ToolkitUtils (laut Release-Notes empfohlen: 7.0.6.0-rc1 „Odyssey“) läuft, oder ob wir ohne Utils planen.
- Welche Befehle im Stream gebraucht werden und ob Kurzformen (`!balance`) eigene Befehle brauchen.
- Alle Wege, auf denen das TTK Preise speichert (Preisinflation darf nicht in die Dateien gelangen).
- Ob Twitch wirklich nur einen festen Kanalpunkte-Preis je Abstimmung kennt.
- Wie die Rollen Verbündete, Händler und Besuch im Spiel am besten entstehen (Stufe 2).

## Serveranforderungen
Entwürfe in `serveranforderungen/`. Anlegen im Server-Repository durch den Nutzer oder eine Session mit Issue-Zugriff; danach hier die Issue-Nummern eintragen.
| Entwurf | Art | Issue |
|---|---|---|
| SA-1 Chat-Befehle und Chatzeilen | Ergänzung zu Issue #74 | – |
| SA-2 Aktive Chatter und Kategorie | neues Issue | – |
| SA-3 Eingehende Raids an Verbraucher | neues Issue | – |
| SA-4 Twitch-Abstimmung im Auftrag eines Verbrauchers | neues Issue | – |
