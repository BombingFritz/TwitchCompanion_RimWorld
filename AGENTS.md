# AGENTS.md – Regeln für KI-Assistenten

## Zuständigkeit
- Dieses Repository enthält die RimWorld-Mod „Twitch Companion for RimWorld“: Quellcode, Mod-Dateien und die Doku der Mod (`docs/`).
- Der Server `BombingFritz/Twitch-Companion-JS-server` gehört nicht zu diesem Repository. Seine Regeln (`AGENTS.md` dort) gelten: **Verbraucher ändern weder Servercode noch Serverdoku** (Specs, ADRs, Fahrplan, Backlog). Braucht die Mod etwas vom Server, wird dort ein Issue mit der Vorlage „Serveranforderung“ angelegt oder ein vorhandenes Issue ergänzt. Entwürfe dafür liegen in `docs/serveranforderungen/`, bis sie angelegt sind.
- Die Schnittstelle des Servers gilt erst, wenn sie in dessen Spec vereinbart ist. Bis dahin sind Vorschläge in diesem Repository als solche gekennzeichnet; die Mod baut nicht auf unvereinbarte Endpunkte.

## Vor jeder Aufgabe
1. Stand auf GitHub prüfen: Branch, Working Tree, offene Pull Requests hier; im Server offene Issues und PRs, die die Mod betreffen.
2. Lesen: `docs/specs/mod.md`, `docs/adr/`, `docs/OFFEN.md`; im Server die vereinbarten Specs, die die Mod nutzt.
3. Nur im vereinbarten Umfang arbeiten; Unklares als `[UNGEKLÄRT]` markieren und nachfragen.

## Konventionen
- RimWorld 1.6, `net472`, Referenzen über `Krafs.Rimworld.Ref` und Harmony; TwitchToolkit und ToolkitCore werden nur referenziert, nicht mitgeliefert.
- TwitchToolkit (TTK) wird nicht geforkt, sondern per Harmony gepatcht. Jeder Patch steht mit Zweck in `docs/specs/mod.md`, Abschnitt „Patches“.
- Spielzustand nur im Haupt-Thread ändern. Netzwerk läuft im Hintergrund und übergibt über eine Warteschlange.
- Keine Tokens, Secrets oder privaten Adressen im Repository; die Mod braucht keinen Twitch-Token.
- Neue Datenflüsse (Netzwerk, Speicherung) im README unter „Daten und Netzwerk“ nennen.

## Lizenz
- AGPL-3.0 (ADR 0001). Übernommener Code aus MIT-Projekten (z. B. ToolkitRaids) behält seinen Lizenzhinweis im Dateikopf und steht in `NOTICE.md`.
- Kein Code aus dem Server hierher und keiner von hier in den Server; gemeinsam ist nur die dokumentierte Schnittstelle.
- Kein Code aus ToolkitCore kopieren (keine Lizenzdatei gefunden), nur zur Laufzeit nutzen.

## Arbeitsweise
- Eigener Branch je Aufgabe, kleine Schritte, ein Commit je logischer Änderung. Gemergt wird nur vom Nutzer.
- Ergebnisse ehrlich trennen: statisch geprüft, automatisiert getestet, im Spiel getestet, im Stream bestätigt.
