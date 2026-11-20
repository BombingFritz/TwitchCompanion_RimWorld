# Architektur (Ist-Zustand)

Version 0.1 (Feature-Issue #2): Anbindung an den Server-Vertrag „Chat-Brücke“ (auf Server-`main` gemergt, siehe unten). Dazu die Wirtschaft (#10, #11): Preis-Werkzeug, Lootbox-Chancen, Pfennig, Wettbonus. Gesamtaufbau: [Spec](specs/mod.md), Abschnitt „Aufbau“ und „Stufe 1“; Entscheidungen in [adr/](adr/).

| Datei | Inhalt |
|---|---|
| `About/About.xml`, `LoadFolders.xml` | Mod-Daten; packageId `BombingFritz.TwitchCompanion.RimWorld`, braucht Harmony und RICS (`Captolamia.RICS`), lädt danach |
| `Source/TwitchCompanion/Core/` | reine Logik ohne RimWorld: `ChatLine`, `LineMerger` (Zeilen zusammenfassen), `PrivacyNotices` (Hinweis an `!privat`-Zuschauer einmal je Stream), `Json` (kleiner JSON-Leser), `SseParser`, `ServerEvents` (Daten des Servers), `CategoryGate` (Kategorie RimWorld, Auswahl aktiver Chatter, doppelte IDs), `ServerLink` (Verbindung zum Server, drei Hintergrund-Threads), `Economy` (Zuschaustunde, Preise, Lootbox-Chancen), `BetBonus` (Wettbonus), `ChatTranslator` (feste RICS-Texte deutsch, kurze `!bal`-Antwort), `Streamlining` (erlaubte Befehle, Kolonisten-Sperre, kleiner Laden, eine Einblendung je Befehl) |
| `Source/TwitchCompanion/Rics/RicsApi.cs` | findet die RICS-Stellen per Reflexion; Eingang (E1), aktive Chatter und Lurker (E2), Präfixe aus den RICS-Einstellungen |
| `Source/TwitchCompanion/Rics/RicsPatches.cs` | Harmony-Patches P1 bis P6 |
| `Languages/German (Deutsch)/Keyed/RICS_Chat.xml` | deutsche Chattexte für RICS |
| `Source/TwitchCompanion/Rics/RicsEconomy.cs` | Preise, Währung und Pfennig in RICS (E3) |
| `Source/TwitchCompanion/Bridge.cs` | Hauptthread-Seite: Ereignisse des Servers an RICS, Ausgang, Testmodus, Statuszähler |
| `Source/TwitchCompanion/TwitchCompanionMod.cs` | Mod-Einstellungen mit Status, Start, `GameComponent` als Hauptthread-Takt |
| `Source/TwitchCompanion.Tests/` | Unit-Tests für `Core/`, `ServerLink` gegen eine Attrappe (`FakeServer`, `HttpListener` auf localhost) |
| `tools/attrappe-server.mjs` | Attrappe des Servers für Spieltests (Node, Port 3999, nur lokal) |

Ablauf:
- Eingang: `ServerLink` (Thread „SSE“) liest `GET /api/chat/stream`, verwirft doppelte `messageId`, legt Ereignisse in `ServerLink.Events` → `Bridge.MainThreadTick` (je Bild bis zu 50 Ereignisse) → Kategorie prüfen → RICS (E1).
- Coins: Thread „Poll“ fragt jede Minute `/api/twitch/channel` und, wenn die Kategorie passt, `/api/chat/active` → Hauptthread → E2.
- Ereignis-Abstimmung: `Bridge.MainThreadTick` → `EventVotes.Tick` (Zeitplan `VoteSchedule`, `EventPlanner`, `VoteRound`) → eigene Liste `EventCatalog` → `IncidentWorker.TryExecute` bzw. `WeatherDecider.StartNextWeather`; P13 merkt fremde Ereignisse. Streambeginn aus `live` (`QueueRules.StreamChanged`). Kein Serveraufruf; das Starten der Twitch-Abstimmung ist blockiert durch Server-Issue #81.
- Streamende: `live` aus `/api/twitch/channel` → Hauptthread → `QueueRules.StreamChanged` (30 min Pause) → RICS-Warteschlange und Liste der Angestellten leeren. Kein neues Serverfeld.
- Wettbonus: derselbe Thread fragt jede Minute `/api/twitch/prediction/current`; bei einer neu aufgelösten Wette in der Kategorie RimWorld zusätzlich `/api/privacy/optouts` → `PredictionEvent` → Hauptthread: Pfennig über E3, eine Chatzeile.
- Ausgang: RICS → P1/P3 → `LineMerger` (1 s Ruhe, bis 500 Zeichen) → `ServerLink.EnqueueSend` → Thread „Send“ → `POST /api/chat/send` (≥ 1 s Abstand, 429 → `retryAfterMs`). Im Testmodus ins Log.
- Logausgaben der Hintergrund-Threads gehen über eine Warteschlange und erscheinen im Hauptthread.

## Serverstand und Integration
Führender Ort für den Stand der Anbindung an den Server `BombingFritz/Twitch-Companion-JS-server` (Regeln in `AGENTS.md`). Nur gemergte, auf `main` des Servers nachgewiesene Fähigkeiten zählen als verfügbar.

**Zuletzt geprüfter Serverstand:** `main` bei Commit `3b4e320` (20.11.2026, Merge von [PR #82](https://github.com/BombingFritz/Twitch-Companion-JS-server/pull/82)), nur auf GitHub gelesen. Die Spec `chat-bruecke.md` ist gegenüber dem angebundenen Entwurf `fbed3e0` unverändert bis auf die neue Karte „Chat-Brücke“ in der Webkonsole (`/api/admin/overview`, für die Mod ohne Bedeutung). Welcher Stand auf dem Streaming-PC läuft, ist hier nicht nachgewiesen.

**Genutzte Verträge (gemergt):**
- [Spec `chat-bruecke.md`](https://github.com/BombingFritz/Twitch-Companion-JS-server/blob/3b4e320/docs/specs/chat-bruecke.md), Teil A (#74) und Teil B (#79), ADR 0025, PR #82 → `3b4e320`. Genutzt: `GET /api/chat/stream?client=rimworld&prefixes=…` (Ereignisse `hello`, `chat`, `privacy`), `POST /api/chat/send` mit `feature: 'rimworld'`, `GET /api/chat/active`, `GET /api/twitch/channel`. Ohne Teil B (404) bleibt die Brücke in der Vorgabe zu (Einstellung „Nur in der Twitch-Kategorie RimWorld“).
- [Spec `wetten-abstimmungen.md`](https://github.com/BombingFritz/Twitch-Companion-JS-server/blob/3b4e320/docs/specs/wetten-abstimmungen.md), Schritt 1: `GET /api/twitch/prediction/current` (Wettbonus, #11). Grenze: `topPredictors` nur bis zu 10 je Ausgang (Twitch).
- [ADR 0022](https://github.com/BombingFritz/Twitch-Companion-JS-server/blob/3b4e320/docs/adr/0022-privat-befehl.md): `GET /api/privacy/optouts` (Wettbonus ohne `!privat`-Zuschauer).

**Teststatus:** Unit-Tests und Attrappen-Tests (`FakeServer`, `tools/attrappe-server.mjs`) für: Verbinden und Anmelden, doppelte Ereignisse, Server nicht erreichbar, Wiederverbindung nach Neustart, 429 mit `retryAfterMs`, fehlende Fähigkeit (älterer Server ohne `/api/twitch/channel`), andere Kategorie, aktive Chatter. Wettbonus: Abfrage nur einmal je Wette, nicht ohne Privat-Liste, nicht außerhalb von RimWorld, ohne Schalter keine Abfrage. Spieltest gegen die Attrappe siehe PR #8. Gegen den echten Server: nicht geprüft.

**Benötigte Fähigkeiten des Servers:**
| Fähigkeit | Kurzname | Server-Issue | Blockiert |
|---|---|---|---|
| Chat-Befehle nach Wort und Präfix empfangen, Chatzeilen über die Absender-Logik senden | SA-1 | [#74](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/74) mit [Ergänzung](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/74#issuecomment-7024624436) | Stufe 1 (#2): angebunden, Vertrag gemergt |
| Liste aktiver Chatter und aktuelle Kategorie | SA-2 | [#79](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/79) | Stufe 1 (#2): angebunden, Vertrag gemergt; Raid-Beitritt (#3) |
| Eingehende Raids melden | SA-3 | [#80](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/80) | Stufe 2 (#3) |
| Twitch-Abstimmung starten und Ergebnis melden | SA-4 | [#81](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/81) | „Chat entscheidet“ in Stufe 2 (#3), Stufe 3 (#4) |

**Legacy-Ausnahmen:** keine (die Mod ruft Twitch nie direkt auf).

## RICS-Stand
RICS wird auf den Branches `1.6` und `1.7` entwickelt, nicht auf `master`. Geprüft am Quellcode von Tag `RICS_1.56` (installierte Version) und Branch `1.6` bei Commit `b825e25` (17.11.2026, Version 1.57): Alle Einstiegspunkte und Patches aus der Spec sind vorhanden. Installiert auf dem Streaming-PC: 1.56. Spieltest mit RICS 1.56 gegen die Attrappe (20.11.2026, isolierter Datenordner, Schnelltest-Karte).
