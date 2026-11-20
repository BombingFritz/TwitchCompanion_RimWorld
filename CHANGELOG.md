# Changelog

Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), Versionen nach [Semantic Versioning](https://semver.org/lang/de/). Versionen stehen nur hier und in Git-Tags (`vX.Y.Z`); Tags und Releases legt Fritz in der GitHub-Oberfläche an.

## [Unveröffentlicht]
### Behoben
- Wettbonus gab es beim ersten Start auch für eine längst beendete Wette; jetzt nur für Wetten, die vor höchstens 10 min endeten.
- Keine Lootbox-Begrüßung mehr, wenn die erste Nachricht des Tages selbst `!openlootbox` ist (Patch P7).

### Hinzugefügt
- Chat kurz und auf Deutsch: deutsche Sprachdatei mit 223 RICS-Chattexten, Umschreibung fest eingebauter englischer Texte, kurze `!bal`-Antwort (Patch P6), Schalter in den Einstellungen.
- Wirtschaft (#10, #11): Knopf „Preise nach Zuschauzeit setzen“ (Items ×10 auf den RICS-Standard, Tiere mindestens 1 h, neuer Kolonist 2 h, Spargel 10 min), Knopf für die Währung „Pfennig“, Lootbox-Chancen (über 150 nur 1 zu 20, Patch P5), Wettbonus (1000 ausgezahlte Kanalpunkte = 1 Zuschaustunde, über den gemergten Endpunkt der Wette und die Privat-Liste). Serverstand auf den gemergten Vertrag der Chat-Brücke (`3b4e320`) nachgetragen.
- Serveranbindung für Stufe 1 (0.1, #2), gegen den **noch nicht gemergten** Server-Vertrag „Chat-Brücke“ (Server-Issues #74, #79): Befehle `!…` und Käufe `$…` über `GET /api/chat/stream`, Antworten gebündelt über `POST /api/chat/send` (Funktion `rimworld`, 429 mit `retryAfterMs`), Wiederverbindung nach 5 s, doppelte Ereignisse verworfen. Coins für plaudernde Zuschauer über `GET /api/chat/active`, nur in der Kategorie RimWorld (`GET /api/twitch/channel`); Patch P4 hält die Coin-Vergabe von RICS sonst an; E2 nimmt Lurkern nach dem Laden die Aktivität. `!privat`-Hinweis einmal je Stream. Status in den Mod-Einstellungen, Befehlszeile `-twitchcompanion-server=…`, Attrappe `tools/attrappe-server.mjs`, Tests der Verbindung gegen eine Attrappe.
- Grundgerüst der Mod (0.1, #2): Mod-Daten, RICS-Zugriff per Reflexion, Patches P1 bis P3, Zusammenfassen der Chatzeilen, Hinweis an `!privat`-Zuschauer, Testmodus mit Testnachricht in den Einstellungen, Unit-Tests. Noch ohne Serveranbindung.
- Planung auf Grundlage von RICS: Spec-Entwurf der Mod, ADR 0001, Regeln für KI-Assistenten nach dem Vorbild des Wett-Widgets, Lizenzseite, PR-Vorlage, offene Punkte, Serveranforderungen als Server-Issues #79, #80, #81 und Ergänzung zu #74 angelegt, Feature-Issues #2 bis #5, Entwürfe entfernt. RICS-Stand auf Branch `1.6` (1.57) geprüft, Spieltest mit RICS 1.56 ohne Twitch-Token.
