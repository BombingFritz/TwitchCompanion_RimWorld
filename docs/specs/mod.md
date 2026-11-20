# Spec: Twitch Companion for RimWorld (Mod)

**Status:** Entwurf (19.11.2026), noch nicht vereinbart. Entscheidungen: [ADR 0001](../adr/0001-ergaenzung-zu-rics.md). Offene Fragen: [OFFEN.md](../OFFEN.md). Grundlage ist RICS (Nutzer, 19.11.2026: „lass uns auf RICS umschwenken“); geprüft am Quellcode von RICS (Branch `1.6`, Version 1.57, Stand in `ARCHITEKTUR.md`); RICS 1.56 startet beim Nutzer ohne Twitch-Token und lädt einen Spielstand ohne Fehler (20.11.2026).

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

**Serverstand:** angebunden an den Entwurf der Server-Spec `docs/specs/chat-bruecke.md` (Teil A = Server-Issue #74, Teil B = #79), mit dem Nutzer vereinbart, aber auf dem Server **noch nicht gemergt**. Anbindung auf ausdrücklichen Auftrag des Nutzers (20.11.2026); gilt als nicht kompatibler Entwurf, bis der Server-PR gemergt ist (AGENTS.md).

### Eingang
- Die Mod verbindet sich mit `GET /api/chat/stream?client=rimworld&prefixes=…` (Server-Sent Events). Angemeldet werden die RICS-Präfixe `Prefix` (Vorgabe `!`) und `BuyPrefix` (Vorgabe `$`), dazu immer `$`, weil RICS `$…` stets als Kauf wertet. Ändern sich die Präfixe in RICS, meldet sich die Mod binnen 5 s neu an.
- Nach Abbruch verbindet sie sich nach 5 s neu; ein neues `hello` heißt: Befehle neu angemeldet. Verpasstes liefert der Server nicht nach. Doppelte `chat`-Ereignisse (gleiche `messageId`) verwirft die Mod.
- Jede Nachricht wird im Hauptthread so an RICS gegeben, wie RICS es nach seinem eigenen Twitch-Empfang tut (`TwitchService.ProcessMessageOnMainThread`, RICS 1.56): `ChatMessageWrapper(Login bzw. Anzeigename, Text, "Twitch", Twitch-User-ID)` → `Viewers.UpdateViewerActivity` → Rollen setzen → `ChatMessageLogger.AddMessage` (Anzeige im RICS-Fenster „Live Chat“) → `ChatCommandProcessor.ProcessMessage`. RICS kennt Zuschauer an der Plattform-ID; das passt zu den IDs des Servers.
- Rollen: RICS liest sie nur aus einer TwitchLib-Nachricht (`Viewer.UpdateTwitchRoles`), die es hier nicht gibt. Die Mod setzt `IsBroadcaster`, `IsModerator`, `IsVip`, `IsSubscriber` aus den Abzeichen direkt am Zuschauer, bei jeder Nachricht (RICS speichert Rollen nicht).
- Broadcaster: Seine Nachrichten kommen vom Server. RICS erkennt den Kanalinhaber für Sperren (`IsChannelOwner`) nur am Kanalnamen in den RICS-Twitch-Einstellungen; ohne Kanalnamen gilt für ihn nur die Rolle Broadcaster.
- Eingänge, die länger als 60 s warten (z. B. Nachricht kam im Hauptmenü an), verwirft die Mod. Höchstens 500 Ereignisse warten.
- Flüstern wird nicht unterstützt.

### Ausgang
- RICS antwortet über `ChatCommandProcessor.SendMessageToUser` → `SendToPlatform` → `TwitchService.SendMessage` (nur, wenn `TwitchService.IsConnected`). P3 greift Antworten in `SendToPlatform` ab, P1 alle übrigen Sendungen von `TwitchService` (Raid-Ansagen, `!joinraid`, Willkommensgruß); P2 meldet `IsConnected` wahr, solange die Brücke aktiv ist.
- Nebenwirkungen von P2 (geprüft am Code von RICS 1.56): Anzeige „Connected“ im RICS-Menü, kein Hinweis „Twitch getrennt“, RICS versucht keine eigene Verbindung (`Connect` bricht ab). Text, den der Streamer ins RICS-Fenster „Live Chat“ tippt, geht über P1 und den Server als Bot-Nachricht in den Chat. Befehle aus diesem Fenster laufen dagegen über das Einstellungsfeld `TwitchSettings.IsConnected` und fallen ohne echte RICS-Verbindung auf eine leere YouTube-Kennung zurück; das Fenster taugt deshalb nicht für Befehlstests.
- Zeilen, die binnen 1 s kommen, verbindet die Mod mit „ · “ zu einer Nachricht bis 500 Zeichen und sendet sie mit `POST /api/chat/send {feature: 'rimworld', message}` (Absender laut Webkonsole „Chat-Absender“, Vorgabe Bot). Mindestens 1 s Abstand; bei 429 nach `retryAfterMs` erneut; bei Netzfehlern liegen lassen und nach Wiederverbindung senden; bei 400/403/415/503 verwerfen (Grund in den Einstellungen). Höchstens 50 wartende Nachrichten, älter als 90 s werden verworfen.

### Zuschauer und Coins
- RICS zahlt alle 2 echte Minuten (`CAPChatInteractive_GameComponent.TryAwardWallClockCoinReward`, nicht im Pausenmodus) an Zuschauer, deren `LastSeen` innerhalb `MinutesForActive` (Vorgabe 30) liegt; RICS prüft dabei weder Verbindung noch Kategorie.
- Plaudernde Zuschauer: Jede Minute holt die Mod `GET /api/chat/active` (Server-Issue #79) und setzt bei diesen Zuschauern nur `LastSeen` auf den Zeitpunkt ihrer letzten Nachricht und die Rollen; die Nachrichtenzählung von RICS bleibt unberührt. Unbekannte Zuschauer legt RICS dabei an (`Viewers.GetViewer`). Es zählen nur Nachrichten seit `categoryChangedAt`, damit Plaudern in „Just Chatting“ nach dem Wechsel keine Coins bringt.
- Nur in der Twitch-Kategorie RimWorld (Nutzer, 19.11.2026): Jede Minute `GET /api/twitch/channel`. Kategorie nicht RimWorld, unbekannt, Server ohne diese Fähigkeit oder Verbindung weg: keine Weiterleitung, keine Auffrischung, und P4 hält die Coin-Vergabe von RICS an. Kommt RimWorld zurück, zahlt RICS einmal nach. Die Prüfung lässt sich in den Einstellungen ausschalten (nur für einen Server ohne Teil B gedacht).
- Lurker bekommen keine Coins (Nutzer, 19.11.2026). RICS setzt beim Laden aller Zuschauer `LastSeen` auf „jetzt“, sodass sonst in den ersten 30 min nach dem Spielstart alle bekannten Zuschauer Coins bekämen. E2 setzt deshalb einmal je Spielstart `LastSeen` aller Zuschauer ohne Nachricht in dieser Sitzung knapp hinter das Fenster zurück.

### `!privat`
- Zuschauer mit `!privat` (Server-ADR 0022) nehmen nicht teil (Nutzer, 19.11.2026): RICS speichert Zuschauer, Coins und Kolonisten über den Stream hinaus. Nachrichten mit `private: true` gibt die Mod nicht an RICS weiter und antwortet höchstens einmal je Stream: „Mit !privat spielst du in RimWorld nicht mit.“ „Je Stream“: Die Liste wird geleert, wenn `/api/twitch/channel` von `live: false` auf `true` wechselt, und bei jedem Spielstart (nur im Arbeitsspeicher). Das Ereignis `privacy` protokolliert die Mod ohne Namen.
- Die Liste aktiver Chatter des Servers enthält keine `!privat`-Zuschauer.
- Rückwirkend (Vorschlag, OFFEN.md #11): Erfährt die Mod vom Server, dass jemand `!privat` gewählt hat, entfernt sie dessen Eintrag in RICS und benennt einen zugeordneten Kolonisten um. Nicht in 0.1. [UNGEKLÄRT] welche RICS-Funktion einen Zuschauer sauber entfernt.

### Einstiegspunkte und Patches (Stufe 1)
| Nr. | Ziel in RICS | Art | Zweck |
|---|---|---|---|
| E1 | `ChatCommandProcessor.ProcessMessage(ChatMessageWrapper)`, `Viewers.UpdateViewerActivity`, `Viewers.GetViewer`, `ChatMessageLogger.AddMessage` (danach Rollen am Zuschauer setzen) | Aufruf | Eingang |
| E2 | `Viewers.All`, `Viewer.LastSeen`, `Viewer.MessageCount`, `Viewers.GetViewerByPlatformId` | Aufruf | aktive Chatter auffrischen; nach dem Spielstart Lurker zurücksetzen |
| P1 | `TwitchService.SendMessage`, `TwitchService.SendWhisperAsync` | Prefix, überspringt | Ausgang über die Mod (Flüstern als öffentliche Antwort) |
| P2 | `TwitchService.IsConnected` (Getter) | Postfix | RICS hält sich für verbunden (Anzeige, Raid-Meldungen) |
| P3 | `ChatCommandProcessor.SendToPlatform` (privat) | Prefix, überspringt für `twitch` | Antworten auf Befehle direkt abgreifen; sichert P2 ab, falls der kleine Getter vom JIT eingebettet wird |
| P4 | `CAPChatInteractive_GameComponent.TryAwardWallClockCoinReward` (privat) | Prefix, überspringt | Coins nur in der Kategorie RimWorld und nur mit Serververbindung |
| P5 | `LootBoxCommandHandler` (alle Methoden mit `Rand.Range(…, LootBoxRandomCoinRange.max)`) | Transpiler | Lootbox-Pfennig nach eigenen Chancen (Abschnitt „Wirtschaft“) |
| P6 | `Commands.ViewerCommands.Bal.Execute` | Postfix | kurze deutsche Antwort auf `!bal` (Abschnitt „Chat auf Deutsch“) |
| E3 | `StoreInventory.AllStoreItems`, `StoreItem.CalculateBasePrice`, `StoreInventory.SaveStoreToJsonImmediate`, `RaceSettingsManager.RaceSettings`/`SaveSettings`, `GlobalSettings.CurrencyName`/`BaseCoinReward`/`LootBoxRandomCoinRange`, `Viewer.GiveCoins`, `Viewers.SaveViewers` | Aufruf | Preis-Werkzeug, Währung, Wettbonus |

Alle Stellen findet die Mod zur Laufzeit per Reflexion (keine Kompilier-Abhängigkeit von RICS). Fehlt eine Pflichtstelle nach einem RICS-Update, bleibt die Brücke aus und die Einstellungen nennen sie; fehlen nur `ChatMessageLogger.AddMessage` oder P4, gibt es eine Warnung im Log. Geprüft an RICS 1.56 (installiert) und 1.57 (Branch `1.6`).

### Einstellungen
Brücke an/aus, Server-Adresse (Vorgabe `http://localhost:3000`; die Befehlszeile `-twitchcompanion-server=…` hat Vorrang, für Tests mit der Attrappe), „Nur in der Twitch-Kategorie RimWorld“ (Vorgabe an), Status (Verbindung, Kategorie, weitergeleitet, gesendet, Coins zuletzt aufgefrischt, letzter Fehler), Testmodus.

**Testmodus:** keine Serververbindung. In den Mod-Einstellungen lässt sich eine Testnachricht mit Login, Text und Rollen einspeisen; sie läuft wie eine Chatnachricht durch RICS. Antworten landen im Log (`[Twitch Companion] Chat ← …`). Die Kategorieprüfung und P4 gelten im Testmodus nicht.

**Im Hintergrund:** RimWorld arbeitet ohne die Option „Im Hintergrund weiterlaufen“ (`runInBackground`, Vorgabe aus) nichts ab, solange das Fenster nicht im Vordergrund ist; Befehle warten dann, ab 60 s Wartezeit verfallen sie. Für den Stream die Option einschalten oder RimWorld im Vordergrund lassen.

### Abnahme Stufe 1
- Ohne Server: Spiel startet ohne Fehler, Status „nicht verbunden“, RICS baut keine Twitch-Verbindung auf.
- Mit Attrappen-Server: `!bal` und `$beer` lösen in RICS das Erwartete aus; die Antwort geht als eine Nachricht an die Attrappe.
- Zwei Zuschauer schreiben ohne Befehl; nach dem Coin-Intervall von RICS haben beide Coins. Kategorie „Just Chatting“: keine Weiterleitung, keine neue Aktivität.
- Zuschauer mit `private: true`: keine Wirkung in RICS, einmal der Hinweis.
- Server neu gestartet: Die Mod verbindet sich von selbst wieder und meldet die Befehle erneut an.

## Wirtschaft (Feature-Issues #10, #11; Nutzer, 20.11.2026)
- **Währung „Pfennig“:** Knopf in den Mod-Einstellungen setzt `CurrencyName` in RICS.
- **Zuschaustunde:** Pfennig, die ein aktiver Zuschauer ohne Boni je Stunde bekommt: `BaseCoinReward` × 30 (RICS zahlt alle 2 min). Mit der RICS-Vorgabe 10 sind das 300. Wer schreibt, bekommt Pfennig; Lurker nichts; Subs, VIPs und Mods bekommen in RICS einen Zuschlag. Mehr Nachrichten bringen nicht mehr Pfennig.
- **Preis-Werkzeug** (Knopf mit Rückfrage; setzt immer vom RICS-Standardpreis aus, also wiederholbar; RICS speichert selbst, damit RICS-Editor und Preisliste die echten Preise zeigen):
  - Items: Standardpreis (≈ Marktwert) × Faktor, Vorgabe **×10** (Bier 12 → 120 ≈ 24 min).
  - Tiere: mindestens 1 Zuschaustunde (mit ×10 schon erfüllt, kleinste Tiere 30 → 300).
  - Spargel: 10 Zuschauminuten je Stück, falls ein Mod ihn bringt (Vanilla hat keinen).
  - Neuer Kolonist: 2 Zuschaustunden für Rasse und Xenotyp „Baseliner“; andere Xenotypen behalten ihr Verhältnis zum alten Rassenpreis. Mit Biotech zählt in RICS der Xenotyp-Preis.
  - Ereignisse, Wetter und Traits bleiben unverändert [UNGEKLÄRT, nach Blick auf die Preisliste entscheiden].
- **Lootbox (P5):** 95 % der Lootboxen bringen 25–150 Pfennig, 5 % (1 zu 20) 151 bis zum Höchstwert aus RICS (Vorgabe 750). Im Mittel etwa 106 Pfennig. Abschaltbar.
- **Wettbonus:** Nach einer aufgelösten Twitch-Wette bekommt jeder Gewinner je 1000 ausgezahlte Kanalpunkte (ganze Auszahlung, nicht Reingewinn) eine Zuschaustunde, gerundet; Verlierer verlieren nichts. Quelle: gemergter Lese-Endpunkt `GET /api/twitch/prediction/current` (Server-Spec `wetten-abstimmungen.md`, Schritt 1), jede Minute abgefragt; Privat-Liste über `GET /api/privacy/optouts`. Nur in der Kategorie RimWorld, nur wenn die Privat-Liste abrufbar ist, je Wette einmal (IDs in den Mod-Einstellungen). Grenze von Twitch: nur die bis zu 10 Tipper mit dem höchsten Einsatz je Ausgang. Eine Chatzeile „Wettbonus: @anna +600 Pfennig, …“.

## Chat auf Deutsch (Nutzer, 20.11.2026: kurz und deutsch, Ausführliches auf der Webseite #9)
- **Sprachdatei** `Languages/German (Deutsch)/Keyed/RICS_Chat.xml`: deutsche, kurze Fassung von 223 Chattexten von RICS 1.56 (allgemeine Befehle, Guthaben, Lootbox, Warteschlange, Item- und Kolonistenkauf, Moderation). Greift, wenn RimWorld auf Deutsch läuft. Fehlende Texte (z. B. `!mypawn`, Operationen, Traits, Wetter, Briefe im Spiel) bleiben vorerst englisch.
- **Fest eingebaute Texte** (Lootbox-Begrüßung, fehlende Berechtigung, Abklingzeit, Namensänderung) stehen in RICS im Code; die Mod schreibt sie beim Senden um (`ChatTranslator`).
- **`!bal`** (P6): „1× Lootbox offen · Guthaben: 100 Pfennig · Karma 🙂“ statt vier Zeilen.
- Schalter „Chat kurz und auf Deutsch“ (Vorgabe an) für P6 und die Umschreibung.
- Falls RICS selbst eine deutsche Übersetzung bekommt, meldet RimWorld doppelte Schlüssel; dann die Datei hier entfernen oder mit RICS abstimmen.

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
- Projekt `Source/TwitchCompanion` (`net472`, `Krafs.Rimworld.Ref` 1.6.4871, `Lib.Harmony` nur als Referenz); RICS wird nicht gebraucht (Reflexion). Bauen mit `dotnet build -c Release` im Ordner `Source`; die DLL landet in `1.6/Assemblies/` (nicht im Repository).
- Unit-Tests in `Source/TwitchCompanion.Tests` (`net8.0`, xUnit) für alles in `Core/`, dazu `ServerLink` gegen eine Attrappe (`FakeServer`, `HttpListener` auf localhost): `dotnet test` im Ordner `Source`.
- Weitere Unit-Tests folgen mit Inflation, Losen der Rolle und Gegenteil-Regel.
- Attrappe für Spieltests: `tools/attrappe-server.mjs` (Node, Vorgabe Port 3999, nur lokal, verweigert Port 3000) nach dem Entwurf „Chat-Brücke“ (#74, #79); später auch #80, #81.
- Spieltest ohne Eingriff in die eigenen Daten: `RimWorldWin64.exe -savedatafolder=<Testordner> -quicktest -twitchcompanion-server=http://localhost:3999`.
- Das RICS-Fenster „Live Chat“ taugt ohne echte RICS-Twitch-Verbindung nicht für Befehlstests (siehe „Ausgang“).

## Status
Entwurf. Version 0.1 (Feature-Issue #2): Serveranbindung gegen den nicht gemergten Server-Vertrag umgesetzt, gegen die Attrappe getestet; Test gegen den echten Server und im Stream offen.
