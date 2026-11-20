# Fehlerhistorie

| Fehler | Ursache | Behebung | Hinweis |
|---|---|---|---|
| Wettbonus für eine alte Wette direkt nach dem ersten Start mit dem echten Server (21.11.2026: „@sacknusse +1.557 Pfennig“ ohne laufende Wette) | Der Server liefert als letzten Stand auch eine längst aufgelöste Wette; die Mod kannte sie noch nicht und hielt sie für neu | Bonus nur für Wetten, die vor höchstens 10 min endeten (`endedAt`) | Test `OldPredictionFromServerMemoryBringsNoBonus` |
| `StoreItems.json` leer (`{"items": {}}`) nach „Preise nach Zuschauzeit setzen“ bzw. „Laden auf diese Auswahl beschränken …“ (20.11.2026, beim Nutzer) | Knopf ohne geladenen Spielstand gedrückt (wahrscheinlich im Hauptmenü): RICS hatte seinen Laden noch nicht geladen, die Mod ließ RICS trotzdem speichern | Beide Werkzeuge brechen bei leerem Laden oder ohne Spielstand ab und speichern nichts | Beim nächsten Laden baut RICS den Laden mit Standardwerten neu auf; danach die Werkzeuge im Spiel erneut ausführen |
| Lootbox-Begrüßung und Lootbox-Ergebnis in einer Nachricht, wenn die erste Nachricht des Tages `!openlootbox` ist | RICS begrüßt bei der ersten Nachricht des Tages, auch wenn sie selbst der Befehl ist | Patch P7 unterdrückt die Begrüßung in diesem Fall | Test `LootboxBegruessung` |
