<!-- Entwurf, NICHT AUF GITHUB ANGELEGT: Kommentar zu Issue #74 im Server-Repository („Chat-Befehle an Widgets weiterleiten und Chat-Nachrichten für Widgets senden“). -->

## Zweiter Verbraucher: RimWorld-Mod (über RICS)

Die Mod `BombingFritz/TwitchCompanion_RimWorld` (privat, später öffentlich; Spec dort `docs/specs/mod.md`) braucht dieselbe Fähigkeit wie CoinFlip: Chat-Befehle empfangen und Zeilen über den Server senden. Sie bindet die RimWorld-Mod RICS an den Server an, damit RICS keine eigene Twitch-Verbindung mit eigenem Token braucht. Geprüfter Server-Stand: `main` @ `0428608`, nur gelesen. Ich ergänze hier statt eines neuen Issues, damit es eine gemeinsame Lösung für beide gibt.

**Was sich gegenüber CoinFlip unterscheidet:**
- **Präfixe statt Wortliste:** RICS hat zwei Präfixe, `!` für Befehle (z. B. `!bal`) und `$` für Käufe (z. B. `$beer`). Kaufnamen gibt es Hunderte, sie ändern sich mit der Modliste. Die Mod braucht deshalb alle Nachrichten, die mit einem angemeldeten Präfix beginnen. Für `!` würde auch eine Wortliste reichen (mehrere Dutzend Befehle, in RICS umbenennbar); für `$` nicht.
- **Felder je Nachricht:** Twitch-User-ID, Login, Anzeigename, Abzeichen (Broadcaster, Moderator, VIP, Subscriber), Text, `message_id`. RICS erkennt Zuschauer an der Plattform-ID und vergibt nach Rolle Extra-Coins.
- **Broadcaster-Nachrichten** müssen ankommen (Admin-Befehle im Spiel). Nachrichten des Bots dürfen nicht ankommen, sonst antwortet das Spiel sich selbst.
- **`!privat`:** Der Nutzer wünscht die einfachste und stabilste Lösung (19.11.2026). Aus Sicht der Mod ist das ein Kennzeichen an jeder weitergeleiteten Nachricht, ohne zweite Abfrage. Die Mod lässt solche Zuschauer ganz aus dem Spiel, weil RICS Coins und Namen über den Stream hinaus speichert. Damit sie bereits gespeicherte Daten auch rückwirkend löschen kann, bräuchte sie zusätzlich ein Ereignis, sobald jemand `!privat` einschaltet (ID und Login), denn dieser Befehl geht nicht an Verbraucher.
- **Senden:** eigene Chat-Funktion (Vorschlag `rimworld`), Absender in „Chat-Absender“ einstellbar, gewünschte Vorgabe Bot. Die Mod fasst Zeilen selbst zusammen und sendet höchstens etwa eine Nachricht pro Sekunde.
- **Kein Browser:** Die Mod ist ein Programm ohne `Origin`-Header; Zugriff nur von `localhost`.

**Akzeptanzkriterien aus Sicht der Mod:**
- [ ] Mit angemeldetem Präfix `$` kommen `$beer` und `$beer 5` an, normale Nachrichten nicht.
- [ ] Angemeldete Befehle mit `!` kommen an; nach erneutem Anmelden gilt die neue Anmeldung.
- [ ] Broadcaster-Nachrichten kommen an, Bot-Nachrichten nicht; jede Nachricht trägt die `!privat`-Kennzeichnung.
- [ ] Senden mit Funktion `rimworld` geht über den in der Webkonsole gewählten Absender; Fehler und Ablehnungen kommen als Antwort zurück.
- [ ] Ein neu gestarteter Server wird von der Mod ohne Zutun wieder genutzt; verpasste Nachrichten müssen nicht nachgeliefert werden.

**Auswirkungen:** blockiert Version 0.1 der Mod (Chat über den Bot). Bis dahin entwickelt die Mod gegen eine Attrappe.
