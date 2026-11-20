# Twitch Companion for RimWorld

RimWorld-Mod, die die Mod **RICS** ([CAP] Rimworld Interactive Chat Service) an den [Twitch Companion JS server](https://github.com/BombingFritz/Twitch-Companion-JS-server) anbindet: Zuschauer spielen über den Chat mit, der Chatbot antwortet für das Spiel, Twitch-Raids kommen mit wechselnden Rollen im Spiel an, Abstimmungen über Spielereignisse laufen als Twitch-Abstimmung. Dazu eine Preisinflation gegen Itemfluten.

**Stand:** Version 0.1 (Stufe 1) als Testversion: Befehle, Käufe, Antworten und Coins laufen über den Server, angebunden an den **noch nicht gemergten** Server-Vertrag „Chat-Brücke“ (Server-Issues #74, #79). Siehe [Spec](docs/specs/mod.md), [ADR 0001](docs/adr/0001-ergaenzung-zu-rics.md), [Offene Punkte](docs/OFFEN.md).

## Voraussetzungen
- RimWorld 1.6, Harmony
- [RICS](https://github.com/ekudram/-cap-RimworldInteractiveChatServices/releases) ab 1.54 (geprüft mit 1.56), **ohne** eingetragenen Twitch-Token
- Twitch Companion JS server auf demselben PC mit der Chat-Brücke (Server-Issues #74 und #79, siehe `docs/ARCHITEKTUR.md`)

## Bauen und Installieren (Testversion)
1. .NET SDK 8 installieren, im Ordner `Source` `dotnet build -c Release` ausführen. Die DLL landet in `1.6/Assemblies/`.
2. `About/`, `1.6/`, `LoadFolders.xml` und `LICENSE` in den Ordner `Mods/TwitchCompanion_RimWorld` von RimWorld kopieren (oder den Repository-Ordner dorthin verlinken). RimWorld muss dafür beendet sein, sonst ist die DLL gesperrt.
3. In RimWorld nach Harmony und RICS laden. In RICS keinen Twitch-Token eintragen.
4. Mod-Einstellungen „Twitch Companion“: Brücke aktiv, Server-Adresse `http://localhost:3000`, „Nur in der Twitch-Kategorie RimWorld“ an. Der Status zeigt Verbindung, Kategorie und Zähler.
5. Für den Stream in den RimWorld-Optionen „Im Hintergrund weiterlaufen“ einschalten, sonst bleiben Befehle liegen, solange RimWorld nicht im Vordergrund ist.

Ohne Server: Testmodus in den Mod-Einstellungen, Testnachrichten einspeisen; Antworten stehen im Log.

## Testen
- Unit- und Verbindungstests: `dotnet test` im Ordner `Source` (Attrappe auf localhost, nie gegen den echten Server).
- Spieltest mit Attrappe: `node tools/attrappe-server.mjs 3999`, RimWorld mit `-twitchcompanion-server=http://localhost:3999` starten (am besten mit eigenem `-savedatafolder=…`, damit RICS-Zuschauerdaten getrennt bleiben). Nachrichten einspeisen mit `POST http://localhost:3999/attrappe/chat {"login":"anna","text":"!bal"}`, gesendete Antworten unter `GET /attrappe/sent`; weitere Steuerung im Dateikopf.

## Daten und Netzwerk
Siehe [docs/LIZENZ.md](docs/LIZENZ.md), Abschnitt „Daten und Verbindungen“. Kurz: nur Verbindung zum Server auf diesem PC, kein Twitch-Token; Zuschauerdaten speichert RICS in seinen eigenen Dateien.

## Lizenz
GNU AGPL-3.0, siehe [LICENSE](LICENSE) und [docs/LIZENZ.md](docs/LIZENZ.md).
