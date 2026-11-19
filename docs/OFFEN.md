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
- RICS-Einstellungen „Log All Chat Messages“ und „Enable Debug Logging“: für die ersten Testläufe an, um Fehler zu finden (Nutzer, 20.11.2026). Danach „Log All Chat Messages“ ausschalten, weil Chatnachrichten sonst in der `Player.log` stehen (`!privat`); Logs mit Chatnachrichten nicht in Issues kopieren.
- Patches P1 bis P4 greifen im Spiel: **bestätigt** gegen die Attrappe (20.11.2026, RICS 1.56): Antworten auf `!bal` und `$beer` kamen über P3 bei der Attrappe an.
- RICS führt das Spielereignis `RaidFriendly` (verbündete Gruppe, Vorgabe aus) – Kandidat für die Raid-Rolle „Verbündete“.
- Rollen, Nebenwirkungen von P2, Auffrischen der Aktivität, Coin-Vergabe außerhalb der Kategorie: **geklärt** am Code von RICS 1.56, Ergebnis in der Spec, Abschnitt „Stufe 1“ (Rollen direkt setzen, P4 hält die Coin-Vergabe an, E2 setzt Lurker nach dem Laden zurück).
- RimWorld-Option „Im Hintergrund weiterlaufen“ (`runInBackground`) ist auf dem Streaming-PC aus: Ohne Fokus arbeitet RimWorld keine Befehle ab, nach 60 s verfallen sie. Für den Stream einschalten oder RimWorld im Vordergrund lassen (Nutzer entscheidet).
- RICS-Fenster „Live Chat“ eignet sich ohne echte RICS-Twitch-Verbindung nicht für Befehlstests (Befehle laufen als leere YouTube-Kennung); getippter Text geht über den Bot in den Chat.
- Kanalname in den RICS-Twitch-Einstellungen (ohne Token) eintragen, damit RICS den Streamer als Kanalinhaber erkennt? Vorher prüfen, dass RICS dann keinen Verbindungsversuch startet.
- Methode, an der RICS den Kaufpreis bestimmt (Inflation).
- Saubere Entfernung eines Zuschauers in RICS (`!privat` rückwirkend).
- Raid-Rollen Verbündete, Händler und Besuch; automatischer Beitritt aller, die im Fenster schreiben.
- Ob Twitch nur einen festen Kanalpunkte-Preis je Abstimmung kennt.
- Status des Steam-Workshop-Eintrags von RICS (laut einer Suchmaschinen-Kopie entfernt); GitHub-Releases als Quelle.

## Serveranforderungen und Feature-Issues
Angelegt am 19.11.2026; Stand und Abhängigkeiten in `ARCHITEKTUR.md`, Abschnitt „Serverstand und Integration“, und in den Feature-Issues #2 bis #5.
