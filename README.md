# Twitch Companion for RimWorld

RimWorld-Mod, die die Mod **Twitch Toolkit** an den [Twitch Companion JS server](https://github.com/BombingFritz/Twitch-Companion-JS-server) anbindet: Zuschauer spielen über den Chat mit, der Chatbot antwortet für das Spiel, Twitch-Raids kommen im Spiel an, Abstimmungen laufen als Twitch-Abstimmung.

**Stand:** Planung, noch kein Code. Siehe [Spec](docs/specs/mod.md), [ADR 0001](docs/adr/0001-eigene-mod-statt-fork.md), [Offene Punkte](docs/OFFEN.md).

## Voraussetzungen (geplant)
- RimWorld 1.6, Harmony
- ToolkitCore und Twitch Toolkit aus dem Release [1.6.0-Odyssey](https://github.com/harleyknd1/twitchtoolkit/releases)
- Twitch Companion JS server auf demselben PC, mit den Fähigkeiten aus `docs/serveranforderungen/`

## Daten und Netzwerk
- Verbindung nur zum Server auf diesem PC (`http://localhost:3000`, einstellbar). Keine Verbindung zu Twitch, kein Twitch-Token.
- Zuschauerdaten (Coins, Karma) speichert weiterhin das Twitch Toolkit in seinen eigenen Dateien.

## Lizenz und Herkunft
GNU AGPL-3.0, siehe [LICENSE](LICENSE). Baut auf Twitch Toolkit (AGPL-3.0, hodlhodl, harleyknd1, SirRandoo u. a.) auf; Teile der Raid-Logik sollen aus ToolkitRaids (MIT, SirRandoo) übernommen werden, Hinweise dann in `NOTICE.md`.
