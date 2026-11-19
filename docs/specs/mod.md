# Spec: Twitch Companion for RimWorld (Mod)

**Status:** Entwurf (19.11.2026), noch nicht vereinbart. Entscheidungen: [ADR 0001](../adr/0001-ergaenzung-zu-rics.md). Offene Fragen: [OFFEN.md](../OFFEN.md). Grundlage ist RICS (Nutzer, 19.11.2026: „lass uns auf RICS umschwenken“); geprüft am Quellcode von RICS, Commit vom 22.10.2026 (Release 1.54), noch nicht im Spiel.

## Ziel
Zuschauer spielen RimWorld über die Mod **RICS** ([CAP] Rimworld Interactive Chat Service, `ekudram/-cap-RimworldInteractiveChatServices`, AGPL-3.0) mit, ohne dass RICS selbst mit Twitch spricht. Alles, was Twitch betrifft, läuft über den Twitch Companion JS server; dessen Chatbot DerBdu antwortet für das Spiel.

Ziele des Nutzers, in der Reihenfolge der Stufen:
1. Der Chatbot übernimmt die Interaktion mit RICS (Befehle, Käufe, Antworten, Coins).
2. Ein Twitch-Raid kommt im Spiel als Gruppe mit den Namen der Raider an, nicht immer feindlich; der Zufall entscheidet und kann eine Abstimmung auslösen.
3. Der Chat beeinflusst Spielereignisse über Twitch-Abstimmungen, angezeigt im eigenen Abstimm-Widget SpargelPoll (Partner-Kanal).
4. Später: Spielereignisse lösen Wetten aus.

Dazu Balancing: Preisinflation (Version 0.1b).

## Abgrenzung
| RICS | diese Mod | Server |
|---|---|---|
| Befehle, Shop, Coins, Karma, Zuschauer-Kolonisten, feindlicher Twitch-Raid mit Beitrittsfenster | Anbindung von RICS an den Server, Preisinflation, Raid-Rollen, Abstimmungen über Spielereignisse | Twitch: Chat empfangen und senden, Raids, Abstimmungen starten, Kategorie, Absender, `!privat` |

Was die Mod vom Server braucht, ist als Serveranforderung angelegt (Server-Issues #74 mit Ergänzung, #79, #80, #81; Übersicht in `docs/ARCHITEKTUR.md`) und gilt erst, wenn der Server es vereinbart und gemergt hat (AGENTS.md, „Freigabe zur Integration“).

## Voraussetzungen
- RimWorld 1.6, Harmony, RICS ab 1.54 (packageId `Captolamia.RICS`). RICS unterstützt auch 1.7; die Mod zunächst nur 1.6.
- In RICS **kein** Twitch-Konto und kein Token eintragen, Twitch-Verbindung aus. YouTube und Kick bleiben unberührt (nicht genutzt).
- Server auf demselben PC (Nutzer, 19.11.2026), `http://localhost:3000`, einstellbar.

## Aufbau
- **Serververbindung** (Hintergrund-Thread): Ereignisströme des Servers (Server-Sent Events) offen halten, nach Abbruch nach 5 s neu verbinden, Aufträge per HTTP POST. Keine Spielobjekte in diesem Thread.
- **Hauptthread-Warteschlange**: eine `GameComponent` arbeitet eingegangene Ereignisse im Spieltakt ab, auch pausiert. Erst dort werden RICS-Funktionen aufgerufen.
- **Einstiegspunkte und Patches** in RICS, siehe unten.
- **Einstellungen** im Spiel: Server-Adresse, Brücke an/aus, Status (verbunden, Kategorie aktiv, letzte Fehlermeldung), Werte für Inflation, Raid und Abstimmungen.

## Stufe 1 (Version 0.1): Chat und Coins über den Server

### Eingang
- Die Mod meldet beim Server ihre Befehle an (Server-Issue #74): alle RICS-Befehle mit dem Präfix `!` (RICS-Einstellung `Prefix`) und alle Nachrichten mit dem Kauf-Präfix `$` (RICS-Einstellung `BuyPrefix`, z. B. `$beer`). Kaufnamen sind zu viele für eine Wortliste, deshalb braucht es Weiterleitung nach Präfix.
- Jede weitergeleitete Nachricht wird im Hauptthread als `ChatMessageWrapper` mit Plattform `twitch`, Login, Anzeigename und Twitch-User-ID gebaut und an `ChatCommandProcessor.ProcessMessage` übergeben, wie es RICS selbst nach dem Empfang tut. Vorher `Viewers.UpdateViewerActivity`. RICS kennt Zuschauer an der Plattform-ID; das passt zu den IDs des Servers.
- Rollen (Moderator, VIP, Subscriber) kommen aus den Abzeichen der Nachricht. [UNGEKLÄRT] wie RICS sie aus der Nachricht liest (`Viewer.UpdateFromMessage`); notfalls setzt die Mod sie am Zuschauer direkt.
- Flüstern wird nicht unterstützt.

### Ausgang
- RICS antwortet über `ChatCommandProcessor.SendMessageToUser` → `SendToPlatform` → `TwitchService.SendMessage`, und zwar nur, wenn `TwitchService.IsConnected` wahr ist. Patch P1 lenkt `TwitchService.SendMessage` (und `SendWhisperAsync` als öffentliche Antwort) zur Mod um; Patch P2 meldet `IsConnected` als wahr, solange die Mod mit dem Server verbunden ist. [UNGEKLÄRT] Nebenwirkungen von P2 an den übrigen Stellen, die `IsConnected` abfragen (4 Stellen laut Code).
- Zeilen, die binnen 1 s kommen, verbindet die Mod mit „ · “ zu einer Nachricht bis 500 Zeichen und sendet sie über den Server (Server-Issue #74, Funktion `rimworld`, Absender laut Webkonsole, Vorgabe Bot). Höchstens 50 wartende Zeilen; lehnt der Server wegen voller Warteschlange ab, nach 2 s erneut.

### Zuschauer und Coins
- RICS vergibt Coins an aktive Zuschauer (`MinutesForActive`), aktiv heißt: hat geschrieben. Der Server leitet aber nur Befehle weiter. Damit auch Zuschauer zählen, die nur plaudern, holt die Mod jede Minute die Liste der aktiven Chatter vom Server (Server-Issue #79) und frischt deren Aktivität in RICS auf. [UNGEKLÄRT] ob dafür ein eigener Aufruf nötig ist, damit die Nachrichtenzählung von RICS nicht verfälscht wird.
- Nur in der Twitch-Kategorie RimWorld (Nutzer, 19.11.2026): Meldet der Server eine andere Kategorie oder ist die Verbindung weg, frischt die Mod keine Aktivität auf und leitet nichts weiter. [UNGEKLÄRT] ob RICS dann trotzdem Coins an kürzlich Aktive verteilt; ggf. die Coin-Vergabe von RICS so lange anhalten.
- Lurker bekommen keine Coins (Nutzer, 19.11.2026). Kein Anreiz zum Spammen: Coins gibt es je Zeitabschnitt für Aktive, nicht je Nachricht.

### `!privat`
- Zuschauer mit `!privat` (Server-ADR 0022) nehmen nicht teil (Nutzer, 19.11.2026): RICS speichert Zuschauer, Coins und Kolonisten über den Stream hinaus. Die Mod gibt ihre Nachrichten nicht an RICS weiter und antwortet höchstens einmal je Stream: „Mit !privat spielst du in RimWorld nicht mit.“
- Rückwirkend (Vorschlag, OFFEN.md): Erfährt die Mod vom Server, dass jemand `!privat` gewählt hat, entfernt sie dessen Eintrag in RICS und benennt einen zugeordneten Kolonisten um. [UNGEKLÄRT] welche RICS-Funktion einen Zuschauer sauber entfernt.

### Einstiegspunkte und Patches (Stufe 1)
| Nr. | Ziel in RICS | Art | Zweck |
|---|---|---|---|
| E1 | `ChatCommandProcessor.ProcessMessage(ChatMessageWrapper)`, `Viewers.UpdateViewerActivity` | Aufruf | Eingang |
| P1 | `TwitchService.SendMessage`, `TwitchService.SendWhisperAsync` | Prefix, überspringt | Ausgang über den Server |
| P2 | `TwitchService.IsConnected` (Getter) | Postfix | RICS antwortet auf Twitch, obwohl es selbst nicht verbunden ist |

### Abnahme Stufe 1
- Ohne Server: Spiel startet ohne Fehler, Status „nicht verbunden“, RICS baut keine Twitch-Verbindung auf.
- Mit Attrappen-Server: `!bal` und `$beer` lösen in RICS das Erwartete aus; die Antwort geht als eine Nachricht an die Attrappe.
- Zwei Zuschauer schreiben ohne Befehl; nach dem Coin-Intervall von RICS haben beide Coins. Kategorie „Just Chatting“: keine Weiterleitung, keine neue Aktivität.
- Zuschauer mit `private: true`: keine Wirkung in RICS, einmal der Hinweis.
- Server neu gestartet: Die Mod verbindet sich von selbst wieder und meldet die Befehle erneut an.

## Version 0.1b: Preisinflation
Wunsch des Nutzers (19.11.2026): Je mehr gekauft wird, desto teurer wird alles; ohne Käufe sinken die Preise wieder.
- Ein gemeinsamer **Preisfaktor** `F ≥ 1` gilt für alle Käufe in RICS.
- Jeder Kauf zählt mit seinem Grundpreis in den **Umsatz** `U`. `U` halbiert sich ohne Käufe alle `H` Minuten.
- `F = min(Fmax, 1 + U / R)`; Startwerte (Vorschlag, OFFEN.md): `H` = 20 min, `R` = 3000 Coins, `Fmax` = 4. Beispiel: Nach 3000 ausgegebenen Coins kostet alles das Doppelte, 20 min ohne Käufe später das 1,5-Fache.
- Umsetzung über einen Patch an der Stelle, an der RICS den Preis eines Kaufs bestimmt, damit Anzeige, Prüfung und Abbuchung denselben Preis nutzen; gespeicherte Preise in RICS bleiben Grundpreise. [UNGEKLÄRT] welche RICS-Methode das ist.
- Befehl für den aktuellen Faktor (Vorschlag `!preise`).

## Stufe 2 (Version 0.2): Raid mit Rollen
- RICS hat einen Twitch-Raid eingebaut: Beitrittsfenster (Vorgabe 240 s, `!joinraid`), Raider mit Zuschauernamen, Stärke nach Koloniewert, Raid über `IncidentWorker_RaidEnemy`, also immer feindlich. Ausgelöst wird er heute von der IRC-Raid-Meldung von RICS; ohne eigene Twitch-Verbindung kommt sie nicht.
- Der Server meldet eingehende Raids (Server-Issue #80). Die Mod lost die **Rolle** nach Gewichten (Nutzer, 19.11.2026: 50/15/10/10/15 für Angriff, Verbündete, Händler, Besuch, „Chat entscheidet“). Bei „Chat entscheidet“ lässt sie den Server eine Twitch-Abstimmung starten (Server-Issue #81, 90 s); ohne Ergebnis lost sie unter den übrigen Rollen.
- **Abstand** (Nutzer, 19.11.2026): Binnen 20 min nach dem letzten Spiel-Raid kommt das Gegenteil: war er feindlich (auch nach Abstimmung), ist der neue verbündet, und umgekehrt; nach Händler oder Besuch normale Gewichtung. Läuft für den letzten noch eine Abstimmung, ohne die Rolle „Chat entscheidet“.
- **Angriff:** RICS-Ablauf über `TwitchService.StartRaidJoinCollection(raider, zuschauer)`. Andere Rollen: eigenes Beitrittsfenster nach demselben Muster und eigene Ereignisse (verbündete Kampfgruppe, Händlerkarawane, Besuchergruppe) mit Zuschauernamen. [UNGEKLÄRT] ob sich RICS-Teile dafür wiederverwenden lassen; im Spiel prüfen.
- Beitritt über den RICS-Befehl `!joinraid`. RICS trägt außerdem alle ein, die im Fenster schreiben; das passiert in RICS nur auf dem eigenen IRC-Weg und muss die Mod mit der Liste aktiver Chatter (Server-Issue #79) nachbilden. [UNGEKLÄRT]

## Stufe 3 (Version 0.3): Abstimmungen über Spielereignisse
- RICS hat keine Abstimmungen. Die Mod bietet in einstellbarem Abstand (Vorschlag 20 min) drei zufällige Ereignisse aus dem RICS-Shop zur Wahl an und lässt den Server eine Twitch-Abstimmung starten (Server-Issue #81). Der Gewinner wird kostenlos ausgelöst. Startet keine Abstimmung (schon eine offen, Server meldet Grund), entfällt die Runde.
- Twitch-Grenzen: 2 bis 5 Optionen, Titel bis 60 und Optionen bis 25 Zeichen, 15 bis 1800 s; die Mod kürzt Namen.
- **Zusatzstimmen** mit Kanalpunkten (Nutzer, 19.11.2026): fest 3000 Kanalpunkte je Zusatzstimme, nur bei etwa jeder dritten Abstimmung. Die Mod schaltet sie je Abstimmung mit Wahrscheinlichkeit 1/3 ein und nennt das in der Ankündigung; gilt für alle Abstimmungen der Mod, auch die Raid-Rolle [Deutung von Claude, Bestätigung offen].

## Stufe 4 (später)
Spielereignisse (Raid beginnt, Kolonist stirbt) an den Server melden; der Server könnte daraus Wetten starten oder Effekte auslösen. Eigene Serveranforderung, wenn es so weit ist. Weiteres Balancing erst nach den ersten Streams.

## Bauen und Testen
- Projekt `net472`, `Krafs.Rimworld.Ref` 1.6, `Lib.Harmony` (nur Referenz), RICS-DLL aus dem lokalen Mods-Ordner über eine Eigenschaft in `Directory.Build.props.user` (nicht im Repository).
- Unit-Tests der reinen Logik (Zeilen verbinden, Inflation, Losen der Rolle, Gegenteil-Regel) ohne RimWorld.
- Attrappen-Server für die Schnittstellen aus den Server-Issues #74, #79, #80, #81 (nur als Entwurf, bis der Server sie vereinbart hat).
- RICS hat ein Fenster „Live Chat“ (`Window_LiveChat`), das Nachrichten durch denselben Ablauf schickt; nützlich für Spieltests ohne Server.

## Status
Entwurf. Kein Code. Serveranforderungen nicht auf GitHub angelegt.
