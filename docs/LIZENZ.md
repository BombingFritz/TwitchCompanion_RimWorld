# Lizenz und Einordnung

Diese Seite beschreibt, unter welcher Lizenz die Mod steht, worauf sie aufbaut und welche Daten sie verarbeitet. Sie ist kein Lizenztext und keine Rechtsberatung; maßgeblich ist der Text in [`LICENSE`](../LICENSE).

## Einordnung
| Frage | Antwort |
|---|---|
| Modul oder Serverkern? | Eigenständige Mod für RimWorld. Keine Änderung am Server. |
| Lizenz | GNU AGPL-3.0, unveränderter Standardtext |
| Warum AGPL | Die Mod wird gegen RICS (AGPL-3.0) gebaut und greift per Harmony in RICS ein (ADR 0001). |
| Gilt nicht für | Den Twitch Companion JS server. Er ist ein eigenes Programm mit eigener Lizenz; die Mod spricht nur über dessen HTTP-Schnittstellen mit ihm. |
| Quellcode | Öffentlich in diesem Repository (erfüllt die Quellcode-Pflicht der AGPL, auch §13). |

## Verhältnis zum Server
- Kein Code aus dem Server, kein Code von hier im Server.
- Genutzte Schnittstellen: „Chat-Brücke“ (Server-Issues #74, #79, gemergt), Stand der Wette, Privat-Liste; nur HTTP auf localhost; Stand in `ARCHITEKTUR.md`.

## Verwendete Bestandteile
| Bestandteil | Herkunft | Lizenz / Hinweis |
|---|---|---|
| Code der Mod | im Auftrag des Nutzers mit einem KI-Assistenten (Claude Code) geschrieben | AGPL-3.0 (dieses Repository) |
| RICS | `ekudram/-cap-RimworldInteractiveChatServices` | AGPL-3.0; zur Laufzeit per Reflexion genutzt, nicht mitgeliefert |
| Harmony | `pardeike/Harmony` | MIT; nur referenziert, zur Laufzeit von der Harmony-Mod |
| RimWorld-Referenzen | NuGet `Krafs.Rimworld.Ref` | nur zum Bauen, nicht mitgeliefert |
| .NET-Framework-Referenzen | NuGet `Microsoft.NETFramework.ReferenceAssemblies` | MIT; nur zum Bauen |
| Testwerkzeuge | NuGet `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | Apache-2.0 bzw. MIT; nur für Tests, nicht mitgeliefert |

Fremder Code: keiner bisher. Die deutsche Sprachdatei übersetzt Texte aus RICS (AGPL-3.0) und steht wie die Mod unter AGPL-3.0.

## Daten und Verbindungen
| Verbindung / Speicherung | Zweck | Daten |
|---|---|---|
| Server auf diesem PC (`http://localhost:3000`, einstellbar) | Befehle empfangen, Chatzeilen senden, Liste aktiver Chatter und Kategorie; später Raids, Abstimmungen | Empfang: Login, Anzeigename, Twitch-User-ID, Abzeichen, Befehlstext (nur `!…`/`$…`), Kennzeichen `!privat`; aktive Chatter mit ID, Login, Name, Zeitpunkt der letzten Nachricht, Abzeichen (ohne Text); Kategorie und Live-Status; Wettbonus: Stand der letzten Wette mit bis zu 10 Tippern je Ausgang (ID, Login, Name, Einsatz, Auszahlung) und die IDs der `!privat`-Liste. Senden: Antworten von RICS, Wettbonus-Zeile mit Logins der Gewinner. Nur im Arbeitsspeicher |
| Server: Abstimmung und Raid (`POST /api/twitch/poll/start`, Ereignisse `poll_result`, `raid`) | Twitch-Abstimmung über Spielereignisse, Raid im Spiel | Senden: Titel und Namen der Ereignisse, Dauer, Kanalpunkte je Zusatzstimme (keine Personendaten). Empfang: Stimmen je Option; raidender Kanal (ID, Login, Name) und Zuschauerzahl. Der Name des raidenden Kanals und der beitretenden Plaudernden (ohne `!privat`, die liefert der Server nicht) erscheinen im Spiel als Raider. Nur im Arbeitsspeicher; RICS selbst merkt sich die Raider-Namen im Spielstand |
| Log (`Player.log`) im Normalbetrieb | Verbindungsstatus, Fehler | keine Namen und kein Chattext von der Mod; `!privat`-Umschalten nur als Anzahl |
| RICS-Dateien (von RICS selbst) | Zuschauer, Coins, Karma, Kolonisten | wie bisher in RICS; Zuschauer mit `!privat` werden nicht eingetragen |
| Mod-Einstellungen | Brücke an/aus, Server-Adresse, Kategorieprüfung, Testmodus, Preisfaktor, Lootbox- und Wettbonus-Schalter, IDs der belohnten Wetten (höchstens 50), zuletzt gesehener Live-Zeitpunkt; später Werte für Inflation, Raid, Abstimmungen | Twitch-User-IDs (ohne Namen) aller, die sich im laufenden Stream angestellt haben (`!joinqueue`, einmal je Stream), und Anzahl der Spam-Verwarnungen je ID; beides geleert 30 min nach Streamende |
| RICS-Zuschauerliste (`viewers.json`, von RICS) | Plaudernde Zuschauer ohne Befehl legt RICS über die Mod an, damit sie Coins bekommen | Login, Twitch-User-ID, Coins, Karma (wie RICS es auch mit eigener Twitch-Verbindung täte) |
| Log (`Player.log`) im Testmodus | Kontrolle der Antworten | RICS-Antworten, oft mit Zuschauernamen; nur lokal |
| Spielstand | Kolonisten tragen Zuschauernamen (RICS) | ohne `!privat`-Zuschauer |

Keine Telemetrie, keine KI- oder Cloud-Dienste, keine Verbindung zu Twitch.
