# Lizenz und Einordnung

Diese Seite beschreibt, unter welcher Lizenz die Mod steht, worauf sie aufbaut und welche Daten sie verarbeitet. Sie ist kein Lizenztext und keine Rechtsberatung; maßgeblich ist der Text in [`LICENSE`](../LICENSE).

## Einordnung
| Frage | Antwort |
|---|---|
| Modul oder Serverkern? | Eigenständige Mod für RimWorld. Keine Änderung am Server. |
| Lizenz | GNU AGPL-3.0, unveränderter Standardtext |
| Warum AGPL | Die Mod wird gegen RICS (AGPL-3.0) gebaut und greift per Harmony in RICS ein (ADR 0001). |
| Gilt nicht für | Den Twitch Companion JS server. Er ist ein eigenes Programm mit eigener Lizenz; die Mod spricht nur über dessen HTTP-Schnittstellen mit ihm. |
| Quellcode | Repository vorerst privat; vor dem ersten Einsatz im Stream öffentlich (AGPL, auch §13). |

## Verhältnis zum Server
- Kein Code aus dem Server, kein Code von hier im Server.
- Genutzte Schnittstellen: noch keine vereinbart (Server-Issues #74, #79, #80, #81; Stand in `ARCHITEKTUR.md`).

## Verwendete Bestandteile
| Bestandteil | Herkunft | Lizenz / Hinweis |
|---|---|---|
| Code der Mod | im Auftrag des Nutzers mit einem KI-Assistenten (Claude Code) geschrieben | AGPL-3.0 (dieses Repository) |
| RICS | `ekudram/-cap-RimworldInteractiveChatServices` | AGPL-3.0; nur referenziert, nicht mitgeliefert |
| Harmony | `pardeike/Harmony` | MIT; nur referenziert, zur Laufzeit von der Harmony-Mod |
| RimWorld-Referenzen | NuGet `Krafs.Rimworld.Ref` | nur zum Bauen, nicht mitgeliefert |

Fremder Code: keiner bisher.

## Daten und Verbindungen
| Verbindung / Speicherung | Zweck | Daten |
|---|---|---|
| Server auf diesem PC (`http://localhost:3000`, einstellbar) | Befehle empfangen, Chatzeilen senden, Liste aktiver Chatter, Raids, Abstimmungen (geplant) | Login, Anzeigename, Twitch-User-ID, Abzeichen, Befehlstext; keine sonstigen Chatnachrichten |
| RICS-Dateien (von RICS selbst) | Zuschauer, Coins, Karma, Kolonisten | wie bisher in RICS; Zuschauer mit `!privat` werden nicht eingetragen |
| Mod-Einstellungen | Server-Adresse, Werte für Inflation, Raid, Abstimmungen | keine Personendaten |
| Spielstand | Kolonisten tragen Zuschauernamen (RICS) | ohne `!privat`-Zuschauer |

Keine Telemetrie, keine KI- oder Cloud-Dienste, keine Verbindung zu Twitch.
