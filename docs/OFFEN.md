# Offene Punkte

## Entscheidungen des Nutzers
| # | Frage | Vorschlag | Betrifft |
|---|---|---|---|
| 1 | Befehle und Coins nur, solange die Twitch-Kategorie „RimWorld“ ist? | ja (wie das UBOAT-Soldbuch im Server) | Stufe 1, SA-2 |
| 2 | Lurker (schreiben nie) bekommen keine Coins – oder soll der Server die Liste aller Anwesenden abfragen (neuer Scope `moderator:read:chatters`)? | keine Coins für Lurker, bremst die Itemflut | Stufe 1, SA-2 |
| 3 | `!privat`-Zuschauer spielen mit, ihre Namen setzt die Mod aber nicht von sich aus ein (z. B. zufällig benannte Raider). | ja | Stufe 1 |
| 4 | Zweiter Raid binnen 20 min: als Besuch oder gar nicht ins Spiel? | Besuch | Stufe 2 |
| 5 | Gewichte der Raid-Rollen Angriff/Verbündete/Händler/Besuch/Abstimmung | 50/15/10/10/15 | Stufe 2 |
| 6 | Beitrittsbefehl für Raider | `!dabei` | Stufe 2 |
| 7 | Kanalpunkte-Stimmen als Ersatz für die Zusatzstimmen von Subs, VIPs, Mods? Wie viele Punkte je Stimme? | [UNGEKLÄRT] | Stufe 3, SA-4 |

## Zu prüfen
- Ob TTK 1.6.0 mit eingeschaltetem ToolkitUtils 2 läuft (nur Quellcode, archiviert) oder ob wir ohne Utils planen.
- Welche Befehle im Stream gebraucht werden und ob Kurzformen (`!balance`) eigene Befehle brauchen.
- Wie die Rollen Verbündete, Händler und Besuch im Spiel am besten entstehen (Stufe 2).
- Ob die TTK-eigene Raid-Benennung nach aktiven Zuschauern (`IncidentWorker_Raid`) überhaupt gewollt ist.

## Serveranforderungen
Entwürfe in `serveranforderungen/`. Anlegen im Server-Repository durch den Nutzer oder eine Session mit Issue-Zugriff; danach hier die Issue-Nummern eintragen.
| Entwurf | Art | Issue |
|---|---|---|
| SA-1 Chat-Befehle und Chatzeilen | Ergänzung zu Issue #74 | – |
| SA-2 Aktive Chatter und Kategorie | neues Issue | – |
| SA-3 Eingehende Raids an Verbraucher | neues Issue | – |
| SA-4 Twitch-Abstimmung im Auftrag eines Verbrauchers | neues Issue | – |
