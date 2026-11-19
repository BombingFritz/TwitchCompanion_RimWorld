# Architektur (Ist-Zustand)

Version 0.1 (Feature-Issue #2): Anbindung an den **noch nicht gemergten** Server-Vertrag „Chat-Brücke“ (Entwurf, siehe unten). Gesamtaufbau: [Spec](specs/mod.md), Abschnitt „Aufbau“ und „Stufe 1“; Entscheidungen in [adr/](adr/).

| Datei | Inhalt |
|---|---|
| `About/About.xml`, `LoadFolders.xml` | Mod-Daten; packageId `BombingFritz.TwitchCompanion.RimWorld`, braucht Harmony und RICS (`Captolamia.RICS`), lädt danach |
| `Source/TwitchCompanion/Core/` | reine Logik ohne RimWorld: `ChatLine`, `LineMerger` (Zeilen zusammenfassen), `PrivacyNotices` (Hinweis an `!privat`-Zuschauer einmal je Stream), `Json` (kleiner JSON-Leser), `SseParser`, `ServerEvents` (Daten des Servers), `CategoryGate` (Kategorie RimWorld, Auswahl aktiver Chatter, doppelte IDs), `ServerLink` (Verbindung zum Server, drei Hintergrund-Threads) |
| `Source/TwitchCompanion/Rics/RicsApi.cs` | findet die RICS-Stellen per Reflexion; Eingang (E1), aktive Chatter und Lurker (E2), Präfixe aus den RICS-Einstellungen |
| `Source/TwitchCompanion/Rics/RicsPatches.cs` | Harmony-Patches P1 bis P4 |
| `Source/TwitchCompanion/Bridge.cs` | Hauptthread-Seite: Ereignisse des Servers an RICS, Ausgang, Testmodus, Statuszähler |
| `Source/TwitchCompanion/TwitchCompanionMod.cs` | Mod-Einstellungen mit Status, Start, `GameComponent` als Hauptthread-Takt |
| `Source/TwitchCompanion.Tests/` | Unit-Tests für `Core/`, `ServerLink` gegen eine Attrappe (`FakeServer`, `HttpListener` auf localhost) |
| `tools/attrappe-server.mjs` | Attrappe des Servers für Spieltests (Node, Port 3999, nur lokal) |

Ablauf:
- Eingang: `ServerLink` (Thread „SSE“) liest `GET /api/chat/stream`, verwirft doppelte `messageId`, legt Ereignisse in `ServerLink.Events` → `Bridge.MainThreadTick` (je Bild bis zu 50 Ereignisse) → Kategorie prüfen → RICS (E1).
- Coins: Thread „Poll“ fragt jede Minute `/api/twitch/channel` und, wenn die Kategorie passt, `/api/chat/active` → Hauptthread → E2.
- Ausgang: RICS → P1/P3 → `LineMerger` (1 s Ruhe, bis 500 Zeichen) → `ServerLink.EnqueueSend` → Thread „Send“ → `POST /api/chat/send` (≥ 1 s Abstand, 429 → `retryAfterMs`). Im Testmodus ins Log.
- Logausgaben der Hintergrund-Threads gehen über eine Warteschlange und erscheinen im Hauptthread.

## Serverstand und Integration
Führender Ort für den Stand der Anbindung an den Server `BombingFritz/Twitch-Companion-JS-server` (Regeln in `AGENTS.md`). Nur gemergte, auf `main` des Servers nachgewiesene Fähigkeiten zählen als verfügbar.

**Zuletzt geprüfter Serverstand:** `main` bei Commit `0428608` (19.11.2026). Der Vertrag liegt im lokalen Server-Checkout auf Branch `feature/chat-bruecke` bei Commit `fbed3e0` (Teil A `aab09ef`, Teil B `fbed3e0`; 20.11.2026, nur gelesen, nicht gemergt, nicht gegen den echten Server getestet). Welcher Stand auf dem Streaming-PC läuft, ist hier nicht nachgewiesen.

**Genutzter Vertrag (Entwurf, nicht kompatibel zugesagt):** Server-Spec `docs/specs/chat-bruecke.md` auf `feature/chat-bruecke`, Teil A (#74) und Teil B (#79). Angebunden auf ausdrücklichen Auftrag des Nutzers (20.11.2026). Genutzt: `GET /api/chat/stream?client=rimworld&prefixes=…` (Ereignisse `hello`, `chat`, `privacy`), `POST /api/chat/send` mit `feature: 'rimworld'`, `GET /api/chat/active`, `GET /api/twitch/channel`. Ohne Teil B (404) bleibt die Brücke in der Vorgabe zu (Einstellung „Nur in der Twitch-Kategorie RimWorld“).

**Teststatus:** Unit-Tests und Attrappen-Tests (`FakeServer`, `tools/attrappe-server.mjs`) für: Verbinden und Anmelden, doppelte Ereignisse, Server nicht erreichbar, Wiederverbindung nach Neustart, 429 mit `retryAfterMs`, fehlende Fähigkeit (älterer Server ohne `/api/twitch/channel`), andere Kategorie, aktive Chatter. Spieltest gegen die Attrappe siehe PR. Gegen den echten Server: nicht geprüft.

**Benötigte Fähigkeiten des Servers:**
| Fähigkeit | Kurzname | Server-Issue | Blockiert |
|---|---|---|---|
| Chat-Befehle nach Wort und Präfix empfangen, Chatzeilen über die Absender-Logik senden | SA-1 | [#74](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/74) mit [Ergänzung](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/74#issuecomment-7024624436) | Stufe 1 (#2): angebunden an Entwurf |
| Liste aktiver Chatter und aktuelle Kategorie | SA-2 | [#79](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/79) | Stufe 1 (#2): angebunden an Entwurf; Raid-Beitritt (#3) |
| Eingehende Raids melden | SA-3 | [#80](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/80) | Stufe 2 (#3) |
| Twitch-Abstimmung starten und Ergebnis melden | SA-4 | [#81](https://github.com/BombingFritz/Twitch-Companion-JS-server/issues/81) | „Chat entscheidet“ in Stufe 2 (#3), Stufe 3 (#4) |

**Legacy-Ausnahmen:** keine (die Mod ruft Twitch nie direkt auf).

## RICS-Stand
RICS wird auf den Branches `1.6` und `1.7` entwickelt, nicht auf `master`. Geprüft am Quellcode von Tag `RICS_1.56` (installierte Version) und Branch `1.6` bei Commit `b825e25` (17.11.2026, Version 1.57): Alle Einstiegspunkte und Patches aus der Spec sind vorhanden. Installiert auf dem Streaming-PC: 1.56. Spieltest mit RICS 1.56 gegen die Attrappe (20.11.2026, isolierter Datenordner, Schnelltest-Karte).
