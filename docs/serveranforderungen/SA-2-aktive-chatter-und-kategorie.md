<!-- Entwurf für ein neues Issue im Server-Repository, Vorlage „Serveranforderung“. Noch nicht angelegt. -->

**Titel:** Serveranforderung: Liste der aktiven Chatter und aktuelle Kategorie für lokale Verbraucher (RimWorld-Coins)

## Bedarf
Die Mod Twitch Toolkit vergibt Coins an Zuschauer, die im Chat aktiv sind. Ihre Zuschauerliste holt sie von `tmi.twitch.tv/group/user/<kanal>/chatters`. Twitch hat diesen Endpunkt 2023 abgeschaltet; seitdem vergibt das Toolkit keine Coins mehr. Die RimWorld-Mod soll die Liste stattdessen vom Server bekommen, der den Chat ohnehin empfängt und Chatter für die Statistik schon kennt.

Außerdem soll das Spiel nur dann reagieren (Befehle, Coins), wenn gerade RimWorld gestreamt wird. Die Kategorie kennt der Server (`currentCategory`).

## Verbraucher und Stand
- Repository und eigenes Feature-Issue: `BombingFritz/TwitchCompanion_RimWorld`, Spec `docs/specs/mod.md` (Stufe 1, „Zuschauer und Coins“)
- Getesteter Server-Stand (Commit oder Release): `main` @ `0428608` (nur gelesen, nicht gegen die Mod getestet)
- Betroffene Spec: keine vorhandene; berührt `statistik.md` (Chatter), `wetten-abstimmungen.md` nicht

## Aktuelles Verhalten
- Im Toolkit: `Viewers.RefreshViewers()` fragt den TMI-Endpunkt jede Minute ab; der liefert keine Liste mehr, `Viewers.jsonallviewers` bleibt `null`, und `Ticker` überspringt die Coin-Vergabe (aus dem Code gelesen, nicht im Spiel beobachtet).
- Im Server: Chatter werden je Stream in `stream_stats.json` gezählt (`noteChatter`), aber keinem Verbraucher angeboten. Die Kategorie steht in `/api/admin/overview`, das nur für die Webkonsole gedacht ist.

## Benötigte Fähigkeit
- Eine Liste der Zuschauer, die in den letzten 60 Minuten im eigenen Kanal geschrieben haben, je Zuschauer: Login, Anzeigename, Zeitpunkt der letzten Nachricht, Moderator/VIP/Subscriber laut Abzeichen, `!privat` ja/nein. Ohne Broadcaster, Bot und die Bots aus `STATS_EXCLUDED_CHATTERS`.
- Aktualisierung etwa jede Minute reicht.
- Die aktuelle Twitch-Kategorie mit Zeitpunkt der letzten Änderung (betrifft auch Issue #78).
- Keine neue Twitch-Abfrage nötig. Wer nie schreibt (Lurker), fehlt; das ist fürs Balancing so gewollt (Entscheidung des Nutzers steht noch aus, siehe Mod-Repository `docs/OFFEN.md`).

Optionaler API-Vorschlag (noch nicht vereinbart):
- Lesen: `GET /api/chat/active` → `{updatedAt, windowMinutes, chatters: [{login, name, lastChatAt, moderator, vip, subscriber, private}]}` und `GET /api/twitch/channel` → `{category, categoryChangedAt, live, updatedAt}`, beide nach dem Muster „letzter Stand ohne Twitch-Anfrage“ (ADR 0023), nur lokal. Die Mod fragt jede Minute.
- Alternativ als Ereignis im Strom aus Issue #74, wenn der Server lieber pusht.

## Akzeptanzkriterien
- [ ] Zwei Zuschauer schreiben; die Liste enthält beide mit Zeitpunkt und Abzeichen der letzten Nachricht.
- [ ] Nach 61 min ohne Nachricht fehlt ein Zuschauer.
- [ ] Broadcaster, Bot und `nightbot` fehlen; Nachrichten aus fremdem Shared-Chat zählen nicht.
- [ ] `!privat`-Zuschauer sind enthalten und gekennzeichnet.
- [ ] Nach einem Kategoriewechsel liefert der Server binnen einer Minute die neue Kategorie.
- [ ] Verhalten bei Fehlern oder Ausfall geklärt: nach Serverstart leere Liste, kein Fehler.
- [ ] Rückwärtskompatibilität: nur neue Endpunkte bzw. Felder.

## Auswirkungen
- Blockiert die Coin-Vergabe in Version 0.1 der Mod. Befehle und Antworten (Issue #74) gehen auch ohne.
- Unabhängige Weiterarbeit: Die Mod kann mit einer Attrappe entwickelt werden.
- Datenschutz (Issue #58): Die Liste liegt nur im Arbeitsspeicher, keine neue Datei nötig.
