# ADR 0001: Ergänzungs-Mod zu RICS, Twitch nur über den Server, AGPL-3.0

**Status:** Entwurf (19.11.2026). Vom Nutzer entschieden: Grundlage RICS statt Twitch Toolkit (19.11.2026), eigenes Repository, Lizenz passend zur Arbeit der anderen, Server als einzige Twitch-Verbindung. Von Claude vorgeschlagen: Anbindung über Einstiegspunkte und Harmony-Patches, Name.

**Kontext:**
- RICS ([CAP] Rimworld Interactive Chat Service, `ekudram/-cap-RimworldInteractiveChatServices`, AGPL-3.0) ist eine gepflegte Neufassung der Idee von Twitch Toolkit: Release 1.54, RimWorld 1.6 und 1.7, Shop, Coins nach Chat-Aktivität, Zuschauer-Kolonisten, feindlicher Twitch-Raid mit Beitrittsfenster. Zuschauer werden an der Plattform-ID erkannt. Abstimmungen über Ereignisse hat RICS nicht.
- RICS verbindet sich selbst mit Twitch (TwitchLib, IRC und Helix, eigener Token). Der Twitch Companion JS server soll die einzige Verbindung zu Twitch sein (dort ADR 0023) und über den Bot DerBdu senden (dort ADR 0017). Verbraucher ändern den Server nicht, sondern stellen Serveranforderungen als Issue (dort AGENTS.md).
- RICS verarbeitet Nachrichten aller Plattformen über einen gemeinsamen Einstieg (`ChatCommandProcessor.ProcessMessage`) und antwortet über eine gemeinsame Stelle (`SendToPlatform`).
- Twitch Toolkit (harleyknd1) ist aus der Steam-Version zurückübersetzt und holt seine Zuschauerliste von einem abgeschalteten Twitch-Endpunkt; ToolkitRaids, ToolkitPolls und Puppeteer gibt es nicht für RimWorld 1.6.

**Entscheidung:**
- Eigene Mod in diesem Repository, die RICS ergänzt: Sie speist Befehle vom Server in RICS ein, leitet die Antworten von RICS an den Server und bringt eigene Funktionen mit (Preisinflation, Raid-Rollen, Abstimmungen). RICS wird nicht geforkt und nicht verändert.
- Twitch nur über den Server: In RICS wird kein Twitch-Token eingetragen.
- Spiellogik bleibt in RICS und der Mod. Vom Server werden nur allgemeine Fähigkeiten angefragt, die auch andere Verbraucher nutzen können.
- Lizenz AGPL-3.0, wie RICS, gegen das die Mod gebaut wird.
- Der Server ist davon nicht betroffen: eigenes Programm, Verbindung nur über die dokumentierte Schnittstelle, kein Code zwischen den Repositories. Abweichung von der Vorgabe „Module unter MIT“, begründet durch RICS.
- Repository vorerst privat (Nutzer, 19.11.2026), spätestens vor dem ersten Einsatz im Stream öffentlich (AGPL, auch §13). Nachtrag 20.11.2026: Das Repository ist öffentlich.

**Verworfene Alternativen:**
- Twitch Toolkit als Grundlage (früherer Entwurf dieses ADR): schwer zu pflegen, Coins ohne Ersatz der Zuschauerliste kaputt, keine Raids.
- RICS mit eigener Twitch-Verbindung: zweiter Token und Absender, keine gemeinsame Drosselung, `!privat` und Bot-Einstellung wirken nicht.
- RICS forken: Wir müssten jede neue RICS-Version übernehmen.
- Das Repository `BombingFritz/itemlist` umbenennen und nutzen: Fork von `SirRandoo/itemlist`, andere Technik und Lizenz.

**Konsequenzen:**
- Die Mod hängt von vereinbarten Serverfähigkeiten ab (SA-1 bis SA-4); vorher nur mit Attrappen-Server testbar.
- Patches und Einstiegspunkte sind an RICS-Versionen gebunden; nach jedem RICS-Update prüfen.
- Fehler oder Wünsche an RICS gehen nach Rückfrage als Issue an RICS.
