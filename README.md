# Twitch Companion for RimWorld

RimWorld-Mod, die die Mod **RICS** ([CAP] Rimworld Interactive Chat Service) an den [Twitch Companion JS server](https://github.com/BombingFritz/Twitch-Companion-JS-server) anbindet: Zuschauer spielen über den Chat mit, der Chatbot antwortet für das Spiel, Twitch-Raids kommen mit wechselnden Rollen im Spiel an, Abstimmungen über Spielereignisse laufen als Twitch-Abstimmung. Dazu eine Preisinflation gegen Itemfluten.

**Stand:** Planung, noch kein Code. Siehe [Spec](docs/specs/mod.md), [ADR 0001](docs/adr/0001-ergaenzung-zu-rics.md), [Offene Punkte](docs/OFFEN.md).

## Voraussetzungen (geplant)
- RimWorld 1.6, Harmony
- [RICS](https://github.com/ekudram/-cap-RimworldInteractiveChatServices/releases) ab 1.54, **ohne** eingetragenen Twitch-Token
- Twitch Companion JS server auf demselben PC, mit den Fähigkeiten aus `docs/serveranforderungen/` (noch nicht vereinbart)

## Daten und Netzwerk
Siehe [docs/LIZENZ.md](docs/LIZENZ.md), Abschnitt „Daten und Verbindungen“. Kurz: nur Verbindung zum Server auf diesem PC, kein Twitch-Token; Zuschauerdaten speichert RICS in seinen eigenen Dateien.

## Lizenz
GNU AGPL-3.0, siehe [LICENSE](LICENSE) und [docs/LIZENZ.md](docs/LIZENZ.md).
