# Spec: Twitch Companion for RimWorld (Mod)

**Status:** Entwurf (19.11.2026), noch nicht vereinbart. Entscheidungen: [ADR 0001](../adr/0001-eigene-mod-statt-fork.md). Offene Fragen: [OFFEN.md](../OFFEN.md).

## Ziel
Zuschauer spielen RimWorld über die Mod **Twitch Toolkit** (TTK, Fork `harleyknd1/twitchtoolkit`, Release „1.6.0 Odyssey“) mit, aber ohne eigene Twitch-Verbindung des Spiels. Alles, was Twitch betrifft, läuft über den Twitch Companion JS server; dessen Chatbot DerBdu antwortet für das Spiel (Nutzer, 19.11.2026).

Ziele des Nutzers, in der Reihenfolge der Stufen:
1. Der Chatbot übernimmt die Interaktion mit dem TTK (Befehle, Antworten, Coins).
2. Ein Twitch-Raid kommt im Spiel als Gruppe mit den Namen der Raider an, nicht immer feindlich: Die Rolle entscheidet der Zufall, und der Zufall kann eine Abstimmung auslösen.
3. Die Abstimmungen des TTK laufen als Twitch-Abstimmung und erscheinen im eigenen Abstimm-Widget SpargelPoll (Partner-Kanal).
4. Später: Spielereignisse lösen Wetten aus; besseres Balancing der Wirtschaft.

## Abgrenzung
| Mod | Server |
|---|---|
| Spiellogik: Befehle ausführen, Coins, Preise, Karma, Raid-Rolle und -Gruppe, wann abgestimmt wird | Twitch: Chat empfangen und senden, Raids, Abstimmungen starten, Kategorie, Absender (Bot/Broadcaster), `!privat` |
| Einstellungen im Spiel | Einstellungen in der Webkonsole |

Was die Mod vom Server braucht, steht in `docs/serveranforderungen/` (SA-1 bis SA-4) und gilt erst, wenn der Server es in seiner Spec vereinbart hat.

## Voraussetzungen
- RimWorld 1.6, Harmony.
- ToolkitCore und TwitchToolkit aus dem Release `1.6.0-Odyssey` von `harleyknd1/twitchtoolkit`. ToolkitUtils ist optional; ob die 1.6-Neufassung („ToolkitUtils 2“) baut und läuft, ist ungeprüft.
- Der Server läuft auf demselben PC (Nutzer, 19.11.2026) unter `http://localhost:3000` (einstellbar).
- In ToolkitCore muss kein Twitch-Konto eingetragen sein; die Mod verhindert die Verbindung (Patch P1).

## Aufbau
- **Serververbindung** (Hintergrund-Thread): hält die Ereignisströme des Servers offen (Server-Sent Events), verbindet bei Abbruch nach 5 s neu, sendet Aufträge per HTTP POST. Keine Spielobjekte in diesem Thread.
- **Hauptthread-Warteschlange**: Eine `GameComponent` arbeitet eingegangene Ereignisse im Spieltakt ab (auch pausiert, über `Update` statt Tick). Erst dort werden TTK-Funktionen aufgerufen.
- **Patches** (Harmony), siehe unten.
- **Einstellungen** (Mod-Einstellungen im Spiel): Server-Adresse, Brücke an/aus, Status (verbunden, Server meldet aktiv/inaktiv, letzte Fehlermeldung), Einstellungen der Stufen 2 und 3.

## Stufe 1 (Version 0.1): Chat und Coins über den Server

### Eingang: Befehle
- Die Mod meldet beim Server alle eingeschalteten Befehle an: `command` aller aktiven `TwitchToolkit.Command`-Defs und, falls vorhanden, der ToolkitCore-Befehle (`ToolkitChatCommand`), dazu „nur Zahlen“ für das Abstimmen im Chat (SA-1). Ändert sich die Liste (Befehlseditor des TTK), meldet die Mod sie neu an.
- Das TTK erkennt Befehle mit `StartsWith("!" + befehl)`; der Server soll nur das erste Wort vergleichen. Kurzformen, die bisher zufällig passten (z. B. `!balance` für `bal`), müssen deshalb eigene Befehle sein. [UNGEKLÄRT], ob im Stream solche Formen benutzt werden.
- Jede eingehende Nachricht wird im Hauptthread als TwitchLib-`ChatMessage` nachgebaut (Name, Farbe, Abzeichen für Mod, VIP, Sub, Broadcaster) und an alle `TwitchInterfaceBase`-Komponenten des Spiels übergeben, wie es ToolkitCore selbst tut. ToolkitCore tut das in einem Hintergrund-Task (`TwitchWrapper.OnMessageReceived`); die Mod macht es im Hauptthread und vermeidet so Zugriffe auf das Spiel aus fremden Threads.
- Flüstern wird nicht unterstützt; die ToolkitCore-Einstellung `forceWhispers` muss aus sein, sonst verarbeitet das TTK keine Chatnachrichten.

### Ausgang: Chatzeilen
- Patch P2 fängt `TwitchWrapper.SendChatMessage` ab. Zeilen, die binnen 1 s kommen (z. B. Abstimmungsoptionen), werden mit „ · “ zu einer Nachricht bis 500 Zeichen verbunden und über den Server gesendet (SA-1, Funktion `rimworld`, Absender laut Webkonsole, Vorgabe Bot).
- Höchstens 50 wartende Zeilen; darüber fallen die ältesten weg (Log-Eintrag). Lehnt der Server wegen voller Warteschlange ab, versucht es die Mod nach 2 s erneut.

### Zuschauer und Coins
- Das TTK holt die Zuschauerliste für die Coin-Vergabe von `tmi.twitch.tv/group/user/<kanal>/chatters` (`Viewers.RefreshViewers`). Twitch hat den Endpunkt 2023 abgeschaltet; ohne Ersatz vergibt das TTK keine Coins (`Ticker`: Bedingung `jsonallviewers != null`).
- Patch P3 schaltet die Abfrage ab. Stattdessen schreibt die Mod die Liste der aktiven Chatter vom Server (SA-2) im alten TMI-Format nach `Viewers.jsonallviewers` und setzt je Zuschauer `last_seen` auf den letzten Chat-Zeitpunkt (nie zurück) sowie Mod, VIP und Sub.
- Befehle und Coins nur, solange die Twitch-Kategorie RimWorld ist (Nutzer, 19.11.2026). Meldet der Server eine andere Kategorie oder ist die Verbindung weg, setzt die Mod `jsonallviewers` auf `null`: Dann gibt es keine Coins, und Befehle kommen ohnehin nicht an.
- Wer nie schreibt (Lurker), bekommt keine Coins (Nutzer, 19.11.2026). Das soll nicht zum Spammen anregen: Coins gibt es je Zeitabschnitt, nicht je Nachricht. Das TTK zahlt voll, wenn die letzte Nachricht höchstens `TimeBeforeHalfCoins` (Vorgabe 30 min) zurückliegt, halb bis `TimeBeforeNoCoins` (60 min), danach nichts. Eine Nachricht je halbe Stunde reicht also; mehr bringt nichts. Diese Regel bleibt so.

### Preisinflation (Version 0.1b)
Wunsch des Nutzers (19.11.2026): Je mehr gekauft und ausgegeben wird, desto teurer wird alles; wird längere Zeit nichts ausgegeben, sinken die Preise wieder.
- Ein gemeinsamer **Preisfaktor** `F ≥ 1` gilt für alle Preise im Shop (Ereignisse, Items, Ticket für die Warteschlange).
- Jeder Kauf zählt mit seinem Grundpreis in den **Umsatz** `U`. `U` schwindet von selbst mit einer Halbwertszeit `H` (Vorschlag 20 min): Ohne Käufe halbiert er sich alle 20 min.
- `F = min(Fmax, 1 + U / R)`. `R` ist der Referenzumsatz (Einstellung, Vorschlag: 3000 Coins); `Fmax` die Obergrenze (Vorschlag 4). Beispiel: Werden in kurzer Zeit 3000 Coins ausgegeben, kostet danach alles das Doppelte; 20 min ohne Käufe später noch das 1,5-Fache.
- Umsetzung: Die Mod merkt sich die Grundpreise (`StoreIncident.cost`, `Item.price`) und schreibt jede Minute und nach jedem Kauf `Grundpreis × F` (gerundet) zurück. So rechnen alle Wege des TTK mit dem aktuellen Preis, auch Kaufbestätigungen und Fehlermeldungen. Vor dem Speichern der Store-Einstellungen setzt sie die Grundpreise zurück, damit die Inflation nicht in die Dateien gelangt [UNGEKLÄRT, im Spiel zu prüfen: alle Speicherwege des TTK].
- Zuschauer sehen den Faktor mit einem Befehl (Vorschlag `!preise`: „Preise gerade ×1,6, sinken ohne Käufe“). Die öffentliche Preisliste zeigt Grundpreise.
- Werte `H`, `R`, `Fmax` in den Mod-Einstellungen; Startwerte nach den ersten Streams anpassen.

### `!privat`
- Zuschauer mit `!privat` (Server, ADR 0022 dort) bleiben vorerst ganz draußen (Nutzer, 19.11.2026). Grund: Das TTK speichert Coins, Karma und Kolonisten-Namen über den Stream hinaus (Zuschauerdatei, Spielstand); genau das schließt `!privat` aus.
- Die Mod ignoriert ihre Befehle und antwortet höchstens einmal je Stream: „Mit !privat spielst du in RimWorld nicht mit.“ Sie bekommen keine Coins und keine Kolonisten, und ihre Namen werden nirgends verwendet (Patch P5 für die Raider-Benennung des TTK).
- Rückwirkend (wie im Server): Meldet der Server, dass jemand `!privat` gewählt hat, löscht die Mod dessen Eintrag in der Zuschauerdatei des TTK, nimmt ihn aus der Warteschlange und gibt einem zugeordneten Kolonisten einen zufälligen Namen (Vorschlag, OFFEN.md).

### Patches Stufe 1
| Patch | Ziel | Zweck |
|---|---|---|
| P1 | `ToolkitCore.TwitchWrapper.StartAsync` | Prefix, überspringt: ToolkitCore verbindet sich nie selbst mit Twitch, solange die Brücke an ist |
| P2 | `ToolkitCore.TwitchWrapper.SendChatMessage` | Prefix, überspringt: Zeile in die Ausgabe der Mod |
| P3 | `TwitchToolkit.Viewers.RefreshViewers` | Prefix, überspringt: keine Abfrage der abgeschalteten TMI-Liste |
| P4 | `TwitchToolkit.PawnQueue.GameComponentPawns.GameComponentTick` | Ersatz: Fehler im Original (löscht aus `pawnHistory`, während es darüber läuft → Ausnahme, sobald ein Zuschauer-Kolonist die Kolonistenleiste verlässt) |
| P5 | `TwitchToolkit.Incidents.IncidentWorker_Raid` (Namensvergabe) | `private` Zuschauer auslassen |

### Abnahme Stufe 1 (im Spiel, Server mit Attrappe oder echt)
- Ohne Server: Spiel startet ohne Fehler, Status „nicht verbunden“, keine Twitch-Verbindung von ToolkitCore.
- `!bal` im Chat: Antwort des TTK kommt vom Bot.
- Neue TTK-Abstimmung: Titel und Optionen erscheinen als eine Chatnachricht; Zahlen im Chat zählen.
- Zwei Zuschauer schreiben, nach dem Coin-Intervall haben beide Coins; ein Zuschauer, der 61 min nichts schreibt, bekommt keine mehr.
- Ein Zuschauer-Kolonist stirbt: kein Fehler im Log (P4).
- Server beendet und neu gestartet: Die Mod verbindet sich von selbst wieder und meldet die Befehle erneut an.

## Stufe 2 (Version 0.2): Raid
- Der Server meldet eingehende Raids mit Name des raidenden Kanals und Zuschauerzahl (SA-3). Die Mod reagiert nur bei laufendem Spiel und ab einer Mindestzahl von Zuschauern (Einstellung, Vorschlag 3).
- **Rolle:** Die Mod lost nach einstellbaren Gewichten: Angriff, Verbündete, Händler, Besuch oder „Chat entscheidet“ (Vorschlag 50/15/10/10/15). Bei „Chat entscheidet“ lässt sie den Server eine Twitch-Abstimmung starten (SA-4), Laufzeit 90 s; ohne Ergebnis oder wenn die Abstimmung nicht startet, lost sie unter den übrigen Rollen.
- Gewichte 50/15/10/10/15 als Startwerte bestätigt (Nutzer, 19.11.2026).
- **Abstand:** Liegt der letzte Spiel-Raid weniger als 20 min zurück, kommt das Gegenteil (Nutzer, 19.11.2026): War der letzte feindlich (auch nach Abstimmung), ist der neue verbündet, und umgekehrt. War der letzte Händler oder Besuch, gilt die normale Gewichtung. Läuft für den letzten Raid noch die Abstimmung, lost die Mod den neuen ohne die Rolle „Chat entscheidet“, weil Twitch nur eine Abstimmung zur Zeit erlaubt.
- **Gruppe:** Der Anführer heißt wie der raidende Kanal. 2 min lang tragen sich Zuschauer mit einem Befehl (Vorschlag `!dabei`) ein; jeder wird ein Mitglied mit seinem Namen. Stärke wie bei ToolkitRaids: Punkte je Person mit Obergrenze, bei feindlichen Raids gegen die Punkte des Storytellers abgeglichen. Logik aus ToolkitRaids (MIT) mit Lizenzhinweis.
- **Umsetzung je Rolle** [UNGEKLÄRT, im Spiel zu prüfen]: Angriff als normaler Raid einer feindlichen Fraktion; Verbündete wie „Militärhilfe“ einer befreundeten Fraktion; Händler als Händlerkarawane; Besuch als Besuchergruppe.
- Einladung und Ergebnis kommen als Chatzeilen über den Ausgang der Mod.

## Stufe 3 (Version 0.3): TTK-Abstimmungen über Twitch
- Patch auf `Vote_VotingIncident.StartVote`: Die Mod lässt den Server eine Twitch-Abstimmung mit Titel und Optionen starten (SA-4). Startet sie, sendet die Mod keine Optionszeilen in den Chat; das Fenster im Spiel bleibt und zeigt „Abstimmung auf Twitch“.
- Patch auf `VoteHandler.CheckForQueuedVotes`: Die Abstimmung endet nicht nach der TTK-Zeit, sondern mit dem Ergebnis vom Server (höchstens Laufzeit plus 30 s, dann entscheidet das TTK selbst). Der Gewinner wird so eingesetzt, dass `DecideWinner` ihn wählt.
- Startet keine Twitch-Abstimmung (schon eine offen, Server meldet Grund): Abstimmung wie bisher im Chat mit Zahlen.
- Twitch-Abstimmungen geben jedem Zuschauer eine Stimme; die Zusatzstimmen des TTK für Subs, VIPs und Mods entfallen.
- Ersatz mit Kanalpunkten (Nutzer, 19.11.2026): Zusatzstimmen sind teuer, damit langjährige Zuschauer mit vielen Punkten nicht alle anderen überstimmen.
  - Preis fest **3000 Kanalpunkte je Zusatzstimme** (Einstellung).
  - Nur bei **etwa jeder dritten Abstimmung**: Die Mod schaltet Kanalpunkte-Stimmen je Abstimmung mit Wahrscheinlichkeit 1/3 ein (Einstellung). „Etwa“ als Zufall gedeutet, nicht als feste Folge; gilt für alle Abstimmungen, die die Mod startet, auch für die Raid-Rolle [Deutung von Claude, Bestätigung offen].
  - Ist sie eingeschaltet, nennt die Mod das in der Ankündigung im Chat.
  - Eine Verdopplung je Stimme ist damit vom Tisch; Twitch kennt je Abstimmung ohnehin nur einen festen Preis (`channel_points_per_vote`).
- Twitch-Grenzen: 2 bis 5 Optionen, Titel bis 60 und Optionen bis 25 Zeichen, 15 bis 1800 s. Das TTK stellt 2 bis 5 Optionen (`VoteOptions`, Vorgabe 3), das passt; zu lange Namen kürzt die Mod (Kategorie oder Typ statt langem Namen).

## Stufe 4 (später): Spielereignisse, Wetten, Balancing
- Spielereignisse (Raid beginnt, Kolonist stirbt) an den Server melden; der Server könnte daraus Wetten starten oder Effekte auslösen. Eigene Serveranforderung, wenn es so weit ist.
- Weiteres Balancing: Die alte Itemliste (`BombingFritz/itemlist`) führte trotz Preisen zu Itemfluten, weil das Einkommen mit der Zahl der Chatter wächst. Die Preisinflation (0.1b) ist der erste Schritt; weitere Ideen (Abklingzeiten je Kategorie, Budget der Kolonie) erst nach den ersten Streams.

## Bauen und Testen
- Projekt `net472`, `Krafs.Rimworld.Ref` 1.6, `Lib.Harmony` (nur Referenz). TTK- und ToolkitCore-DLLs aus dem lokalen Mods-Ordner über eine Eigenschaft in `Directory.Build.props` (Pfad nicht im Repository).
- Reine Logik (Zeilen verbinden, TMI-Format, Befehlsliste, Losen der Rolle) ohne RimWorld als Unit-Tests.
- Ein kleiner Attrappen-Server zum Testen im Spiel, ohne echten Twitch-Chat.

## Status
Entwurf. Kein Code. Serveranforderungen noch nicht angelegt.
