<!-- Entwurf: Kommentar zu Issue #74 im Server-Repository („Chat-Befehle an Widgets weiterleiten und Chat-Nachrichten für Widgets senden“). Noch nicht gepostet. -->

## Zweiter Verbraucher: RimWorld-Mod

Die Mod `BombingFritz/TwitchCompanion_RimWorld` (privat, später öffentlich; Spec dort `docs/specs/mod.md`) braucht dieselbe Fähigkeit wie CoinFlip: Chat-Befehle empfangen und Zeilen über den Server senden. Sie ersetzt die eigene Twitch-Verbindung der Mod Twitch Toolkit (ToolkitCore, IRC mit eigenem Token). Getesteter Server-Stand: `main` @ `0428608`. Ich ergänze hier statt eines neuen Issues, damit es eine gemeinsame Lösung für beide gibt.

**Was sich gegenüber CoinFlip unterscheidet:**
- **Viele, frei benennbare Befehle:** Toolkit und ToolkitUtils bringen Dutzende Befehle mit (die alte Befehlsliste des Kanals hatte rund 40), Streamer können eigene anlegen. Die Mod meldet die aktuelle Liste an und bei Änderungen neu. Grobe Obergrenze: 200 Befehle.
- **Abstimmen mit Zahlen:** Das Toolkit zählt Chatnachrichten, die nur aus einer Zahl bestehen (`1`, `2`, `3`), als Stimme. Diese Nachrichten braucht die Mod ebenfalls, zumindest so lange, bis Abstimmungen über Twitch laufen (eigene Anforderung).
- **Felder je Nachricht:** Login, Anzeigename, Namensfarbe, Abzeichen (Broadcaster, Moderator, VIP, Subscriber), Text, `message_id`. Das Toolkit setzt daraus Mod-, VIP- und Sub-Rechte und Extra-Coins.
- **Broadcaster-Nachrichten** müssen ankommen (Admin-Befehle im Spiel). Nachrichten des Bots dürfen nicht ankommen, sonst antwortet das Spiel sich selbst.
- **Senden:** eigene Chat-Funktion (Vorschlag `rimworld`), Absender in „Chat-Absender“ einstellbar, gewünschte Vorgabe Bot. Die Mod fasst Zeilen selbst zusammen (z. B. Abstimmungsoptionen in einer Nachricht) und sendet höchstens etwa eine Nachricht pro Sekunde.
- **Kein Browser:** Die Mod ist ein Programm ohne `Origin`-Header; Zugriff nur von `localhost`.

**Akzeptanzkriterien aus Sicht der Mod:**
- [ ] Eine angemeldete Befehlsliste mit 100 Einträgen funktioniert; nach erneutem Anmelden gilt die neue Liste.
- [ ] Nachrichten nur aus einer Zahl kommen an, wenn die Mod das anfordert.
- [ ] Broadcaster-Nachrichten kommen an, Bot-Nachrichten nicht.
- [ ] Senden mit Funktion `rimworld` geht über den in der Webkonsole gewählten Absender; Fehler und Ablehnungen kommen als Antwort zurück.
- [ ] Ein neu gestarteter Server wird von der Mod ohne Zutun wieder genutzt (Wiederverbinden), verpasste Nachrichten müssen nicht nachgeliefert werden.

**Auswirkungen:** blockiert Version 0.1 der Mod (Chat über den Bot). Bis dahin kann die Mod nur gegen eine Attrappe entwickelt werden.

Offene Frage aus #74, aus Sicht der Mod: Ein Feld je Nachricht, ob der Zuschauer `!privat` gewählt hat, wäre für die Mod am einfachsten.
