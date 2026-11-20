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

## Entschieden (Nutzer, 20.11.2026)
| Frage | Entscheidung |
|---|---|
| Item-Preise | Faktor ×10 auf den RICS-Standardpreis (statt ×12,5); teuerste Sachen nach Blick auf die Preisliste prüfen |
| Spargel | 10 Zuschauminuten (gibt es nur mit Mod) |
| Lootbox | Höhere Gewinne seltener: über 150 nur 1 zu 20 |
| Währung | Pfennig |
| Warteschlange zum Übernehmen | kostet nichts; Rauswurf nach über 1 h ohne Chat, Wette oder Einlösung – sonst „zuletzt hinzugefügt oben“, Leeren nach jedem Stream (ersetzt am 20.11.2026, siehe „Ausmisten“) |
| Wettbonus | ganze Auszahlung zählt (hohe Wetten belohnen) |
| Chatnachrichten | kurz und auf Deutsch, Ausführliches auf der Webseite (#9) |

## Entschieden (Nutzer, 20.11.2026, Ausmisten)
| Frage | Entscheidung |
|---|---|
| Antworten | nur im Spiel, nicht im Chat |
| Befehle | stark ausdünnen; `!bal`, `!openlootbox`, Ereignisse, Wetter, Anpassen des eigenen Kolonisten entfallen |
| Lootbox | automatisch beim ersten Befehl öffnen, danach Guthaben zeigen |
| Laden | nur Dosenbrot, Rausch, Ei, Kartoffeln, Flausch, Holz, Schoki, Hausmittel (Schoki und Hausmittel am 20.11.2026 ergänzt, keine Notration; „Holz vor der Hütte“ am 20.11.2026 auf „Holz“ gekürzt, damit `$Holz` in jeder Spielsprache greift) |
| Neue Kolonisten | zufällig, nach dem Zuschauer benannt, höchstens einer je 30 min; Lebenslauf später per Abstimmung |
| Ereignisse | nicht kaufbar; später vom Erzähler vorgeschlagen, der Chat wählt |
| Warteschlange | nur `!joinqueue`; wer sich zuletzt anstellt, steht vorne; einmal je Stream; nach dem Stream geleert; Vergeben ohne `!acceptpawn`; kein Rauswurf wegen Inaktivität (verworfen, #12) |
| `!leave` | bleibt erlaubt; Kolonist bleibt als „Dulli“ (durchnummeriert) oder verlässt die Kolonie unter seinem Namen; später entscheidet eine Abstimmung (blockiert durch Server-Issue #81) |
| Ein Kolonist je Person | zu jeder Zeit; nach Anstellen, Bekommen und `!leave` im selben Stream kein neues Anstellen |
| Spam | über 5 Befehle je Minute oder über 10 je 10 min = Verwarnung mit Hinweis; nach 3 bis Streamende ignoriert; nach dem Stream gelöscht |
| Flüstern | späteres Ziel |
| Anleitung/Preisliste | wird angepinnt bzw. von Hand gepostet, kein Befehl dafür |

## Noch zu entscheiden
| # | Frage | Vorschlag | Betrifft |
|---|---|---|---|
| 9 | „Etwa jede dritte Abstimmung“: Zufall 1/3 und auch für die Raid-Abstimmung? | ja, beides | Stufe 2/3 |
| 10 | Startwerte der Preisinflation: Halbwertszeit, Referenzumsatz, Obergrenze | 20 min, 3000 Coins, ×4 | 0.1b |
| 11 | `!privat` rückwirkend: Zuschauer in RICS entfernen, Kolonist umbenennen | ja | 0.1 |
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
- Methode, an der RICS den Kaufpreis bestimmt (Inflation): Es gibt keine zentrale; die Befehle lesen `StoreItem.BasePrice` bzw. den Rassen-/Xenotyp-Preis direkt (RICS 1.56). Die Grundpreise setzt jetzt das Preis-Werkzeug; die Inflation braucht einen eigenen Weg.
- Ereignis-, Wetter- und Trait-Preise mit anheben?
- Restliche RICS-Chattexte übersetzen (`!mypawn`, Operationen, Traits, Wetter, Heilen, Wiederbeleben, Ereignisse) und Briefe im Spiel; oder deutsche Übersetzung bei RICS einreichen (nur nach Rückfrage).
- Saubere Entfernung eines Zuschauers in RICS (`!privat` rückwirkend).
- Raid-Rollen Verbündete, Händler und Besuch; automatischer Beitritt aller, die im Fenster schreiben.
- Ob Twitch nur einen festen Kanalpunkte-Preis je Abstimmung kennt.
- Status des Steam-Workshop-Eintrags von RICS (laut einer Suchmaschinen-Kopie entfernt); GitHub-Releases als Quelle.

## Serveranforderungen und Feature-Issues
Angelegt am 19.11.2026; Stand und Abhängigkeiten in `ARCHITEKTUR.md`, Abschnitt „Serverstand und Integration“, und in den Feature-Issues #2 bis #5.
