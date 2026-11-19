# ADR 0001: Eigene Mod mit Harmony statt Fork, Twitch nur über den Server, AGPL-3.0

**Status:** Entwurf (19.11.2026). Vom Nutzer vorgegeben: Ziele, eigenes Repository, Lizenz passend zur Arbeit der anderen, Server als einzige Twitch-Verbindung. Von Claude vorgeschlagen: Aufbau, Harmony statt Fork, Name.

**Kontext:**
- Twitch Toolkit (TTK, `harleyknd1/twitchtoolkit`, AGPL-3.0) bringt Coins, Shop, Abstimmungen und Zuschauer-Kolonisten. Es verbindet sich über ToolkitCore selbst mit dem Twitch-Chat (IRC, eigener Token, eigener Absender).
- Der Twitch Companion JS server ist die einzige Verbindung zu Twitch (dort ADR 0023) und sendet über den Bot DerBdu (dort ADR 0017). Verbraucher ändern den Server nicht, sondern geben Serveranforderungen als Issue ab (dort AGENTS.md).
- Der TTK-Quellcode ist aus der Steam-Version zurückübersetzt. Die Zuschauerliste des TTK kommt von einem seit 2023 abgeschalteten Twitch-Endpunkt.
- ToolkitRaids (MIT) unterstützt höchstens RimWorld 1.5, ToolkitPolls höchstens 1.4, Puppeteer höchstens 1.2; alle archiviert oder ohne Pflege (geprüft an `About.xml` und Git-Verlauf, 19.11.2026).

**Entscheidung:**
- Eigene Mod in diesem Repository. Sie patcht TTK und ToolkitCore per Harmony und bleibt bei neuen TTK-Versionen klein anzupassen.
- Twitch nur über den Server: ToolkitCore verbindet sich nicht; Befehle, Zuschauerliste, Raids und Abstimmungen kommen vom Server, Chatzeilen gehen an ihn.
- Spiellogik bleibt in TTK und Mod (Coins, Preise, Raid-Rolle). Vom Server werden nur allgemeine Fähigkeiten angefragt, die auch andere Verbraucher nutzen können.
- Lizenz AGPL-3.0, weil die Mod gegen das TTK (AGPL-3.0) gebaut wird und es verändert. MIT-Code aus ToolkitRaids darf mit Lizenzhinweis übernommen werden. Kein Code aus ToolkitCore (keine Lizenzdatei gefunden).
- Der Server ist davon nicht betroffen: eigenes Programm, Verbindung nur über die dokumentierte Schnittstelle, kein Code zwischen den Repositories. Abweichung von der Vorgabe „Module unter MIT“ im Fahrplan des Servers, begründet durch das TTK.
- Repository vorerst privat (Nutzer, 19.11.2026). Spätestens vor dem ersten Einsatz im Stream öffentlich, damit die Pflicht zum Quellcode (AGPL, auch §13) sicher erfüllt ist.

**Verworfene Alternativen:**
- TTK unverändert mit eigener Twitch-Verbindung: zweiter Token und Absender, keine gemeinsame Drosselung, `!privat` und Bot-Einstellung wirken nicht, keine Coins ohne Zuschauerliste.
- TTK forken: zurückübersetzter Code, jede neue TTK-Version müsste übernommen werden.
- Das Repository `BombingFritz/itemlist` umbenennen und nutzen: Fork von `SirRandoo/itemlist` (bleibt als Fork markiert), andere Lizenz und Technik; die Preisliste für Zuschauer wird weiter gebraucht.
- Wirtschaft in den Server verlegen: Spiellogik im Server; bleibt eine spätere Möglichkeit.

**Konsequenzen:**
- Die Mod hängt von vereinbarten Serverfähigkeiten ab (Serveranforderungen SA-1 bis SA-4); vorher nur mit Attrappen-Server testbar.
- Patches sind an TTK- und ToolkitCore-Versionen gebunden; nach Updates prüfen.
- Hinweise auf Herkunft übernommener Teile in `NOTICE.md`.
