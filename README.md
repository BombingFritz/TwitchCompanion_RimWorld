# Twitch Companion for RimWorld

RimWorld-Mod, die die Mod **RICS** ([CAP] Rimworld Interactive Chat Service) an den [Twitch Companion JS server](https://github.com/BombingFritz/Twitch-Companion-JS-server) anbindet: Zuschauer spielen über den Chat mit, der Chatbot antwortet für das Spiel, Twitch-Raids kommen mit wechselnden Rollen im Spiel an, Abstimmungen über Spielereignisse laufen als Twitch-Abstimmung. Dazu eine Preisinflation gegen Itemfluten.

**Stand:** Version 0.1 im Aufbau: Grundgerüst mit Testmodus, noch ohne Serveranbindung. Siehe [Spec](docs/specs/mod.md), [ADR 0001](docs/adr/0001-ergaenzung-zu-rics.md), [Offene Punkte](docs/OFFEN.md).

## Voraussetzungen (geplant)
- RimWorld 1.6, Harmony
- [RICS](https://github.com/ekudram/-cap-RimworldInteractiveChatServices/releases) ab 1.54, **ohne** eingetragenen Twitch-Token
- Twitch Companion JS server auf demselben PC, mit den Fähigkeiten aus den Server-Issues #74, #79, #80, #81 (noch nicht vereinbart, siehe `docs/ARCHITEKTUR.md`)

## Bauen und Installieren (Testversion)
1. .NET SDK 8 installieren, im Ordner `Source` `dotnet build -c Release` ausführen. Die DLL landet in `1.6/Assemblies/`.
2. Den Repository-Ordner (mit `About/`, `LoadFolders.xml`, `1.6/`) in den `Mods`-Ordner von RimWorld legen oder dorthin verlinken.
3. In RimWorld nach Harmony und RICS laden. In den Mod-Einstellungen „Twitch Companion“ den Testmodus einschalten und Testnachrichten einspeisen; Antworten stehen im Log.

Tests: `dotnet test` im Ordner `Source`.

## Daten und Netzwerk
Siehe [docs/LIZENZ.md](docs/LIZENZ.md), Abschnitt „Daten und Verbindungen“. Kurz: nur Verbindung zum Server auf diesem PC, kein Twitch-Token; Zuschauerdaten speichert RICS in seinen eigenen Dateien.

## Lizenz
GNU AGPL-3.0, siehe [LICENSE](LICENSE) und [docs/LIZENZ.md](docs/LIZENZ.md).
