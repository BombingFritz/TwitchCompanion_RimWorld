<!-- Entwurf, NICHT AUF GITHUB ANGELEGT: neues Issue im Server-Repository, Vorlage „Serveranforderung“. -->

**Titel:** Serveranforderung: Liste der aktiven Chatter und aktuelle Kategorie für lokale Verbraucher (RimWorld-Coins)

## Bedarf
Die RimWorld-Mod RICS vergibt Coins an Zuschauer, die in den letzten Minuten im Chat aktiv waren, auch wenn sie keine Befehle schreiben. Über den Chat-Kanal aus Issue #74 bekommt die Mod aber nur Befehle. Damit plaudernde Zuschauer weiter Coins bekommen, braucht sie die Liste der aktiven Chatter vom Server, der den Chat ohnehin empfängt und Chatter für die Statistik schon kennt. Dieselbe Liste nutzt RICS beim Twitch-Raid: Wer während des Beitrittsfensters schreibt, wird Raider.

Außerdem soll das Spiel nur reagieren, wenn gerade RimWorld gestreamt wird (Nutzer, 19.11.2026). Die Kategorie kennt der Server (`currentCategory`).

## Verbraucher und Stand
- Repository und eigenes Feature-Issue: `BombingFritz/TwitchCompanion_RimWorld`, Spec `docs/specs/mod.md` (Stufe 1, „Zuschauer und Coins“; Stufe 2); Feature-Issue noch nicht angelegt
- Getesteter Server-Stand (Commit oder Release): `main` @ `0428608` (nur gelesen)
- Betroffene Spec: keine vorhandene; berührt `statistik.md` (Chatter)

## Aktuelles Verhalten
Chatter werden je Stream in `stream_stats.json` gezählt (`noteChatter`), aber keinem Verbraucher angeboten. Die Kategorie steht in `/api/admin/overview`, das nur für die Webkonsole gedacht ist.

## Benötigte Fähigkeit
- Eine Liste der Zuschauer, die in den letzten 60 Minuten im eigenen Kanal geschrieben haben, je Zuschauer: Twitch-User-ID, Login, Anzeigename, Zeitpunkt der letzten Nachricht, Moderator/VIP/Subscriber laut Abzeichen. Ohne Broadcaster, Bot, die Bots aus `STATS_EXCLUDED_CHATTERS` und ohne Zuschauer mit `!privat` (die spielen nicht mit, Nutzer, 19.11.2026).
- Aktualisierung etwa jede Minute reicht.
- Die aktuelle Twitch-Kategorie mit Zeitpunkt der letzten Änderung (betrifft auch Issue #78). Ob der Server die Kategorie prüft oder die Mod, entscheidet der Server.
- Keine neue Twitch-Abfrage nötig. Wer nie schreibt (Lurker), fehlt; das ist fürs Balancing gewollt (Nutzer, 19.11.2026).

Optionaler API-Vorschlag (noch nicht vereinbart): `GET /api/chat/active` → `{updatedAt, windowMinutes, chatters: [{id, login, name, lastChatAt, moderator, vip, subscriber}]}` und `GET /api/twitch/channel` → `{category, categoryChangedAt, live, updatedAt}`, beide nach dem Muster „letzter Stand ohne Twitch-Anfrage“ (ADR 0023), nur lokal. Alternativ als Ereignis im Strom aus Issue #74.

## Akzeptanzkriterien
- [ ] Zwei Zuschauer schreiben; die Liste enthält beide mit Zeitpunkt und Abzeichen der letzten Nachricht.
- [ ] Nach 61 min ohne Nachricht fehlt ein Zuschauer.
- [ ] Broadcaster, Bot, `nightbot` und `!privat`-Zuschauer fehlen; Nachrichten aus fremdem Shared-Chat zählen nicht.
- [ ] Nach einem Kategoriewechsel liefert der Server binnen einer Minute die neue Kategorie.
- [ ] Verhalten bei Fehlern oder Ausfall geklärt: nach Serverstart leere Liste, kein Fehler.
- [ ] Rückwärtskompatibilität: nur neue Endpunkte bzw. Felder.

## Auswirkungen
- Blockiert die Coins für plaudernde Zuschauer und die Kategorieprüfung in Version 0.1 sowie den automatischen Raid-Beitritt in Stufe 2. Befehle (Issue #74) gehen auch ohne.
- Unabhängige Weiterarbeit mit einer Attrappe möglich.
- Datenschutz (Issue #58): nur im Arbeitsspeicher, keine neue Datei nötig.
