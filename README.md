# Arbor – Aufgaben und Dokumente

Eine ASP.NET Core MVC Anwendung mit Entity Framework Core und SQLite. Die Oberfläche verwendet nur eigenes CSS und JavaScript. Es gibt derzeit keine Anmeldung; die App ist für den lokalen Betrieb gedacht.

## Starten

Voraussetzung ist nur das .NET 10 SDK. Zum Starten reicht:

```powershell
dotnet run
```

Danach `http://localhost:5222` öffnen. Beim ersten Start legt die App die Datenbank `.local/arbor.db` an, führt die EF Core Migration aus und erstellt Beispiel-Felder und -Ansichten. Bilder aus Beschreibungen landen in `.local/images`. Der Ordner `.local` ist von Git ausgeschlossen.

## Speicherorte

Über **Einstellungen** oben rechts lassen sich der Pfad der Datenbank-Datei und der Bilder-Ordner ändern. Pfade können absolut oder relativ zum Programmordner sein; Umgebungsvariablen wie `%USERPROFILE%` werden aufgelöst. Die Auswahl wird in `.local/storage.json` gespeichert, nicht in der Datenbank selbst.

- Liegt am neuen Datenbank-Pfad noch keine Datei, wird die aktuelle Datenbank dorthin verschoben.
- Liegt dort bereits eine Arbor-Datenbank, wird sie verwendet (und bei Bedarf migriert); die bisherige Datei bleibt am alten Ort. Dateien anderer Programme werden abgelehnt.
- Bilder werden in den neuen Ordner verschoben. Dabei werden nur Dateien angefasst, die die App selbst angelegt hat.

Die Datenbank ist eine einzelne Datei (ohne WAL-Nebendateien). Für ein Backup genügt es, sie zusammen mit dem Bilder-Ordner zu kopieren.

## Funktionen

- Aufgaben mit beliebig tiefen Unteraufgaben, Status, formatierter Beschreibung sowie Start- und Fälligkeitsdatum
- Textdokumente im gemeinsamen Baum: Dokumente können unter Aufgaben stehen und ihrerseits Aufgaben oder weitere Dokumente enthalten
- Ordner mit Titel und Beschreibung für Aufgaben, Dokumente und Unterordner; Ordner können nur auf der obersten Ebene oder unter Ordnern liegen, auch beim Verschieben per Drag & Drop
- Zusätzliche Verknüpfungen zwischen Aufgaben und Dokumenten; derselbe Text kann mehreren Aufgaben zugeordnet werden, ohne ihn zu kopieren
- Zugehörige Dokumente bzw. Aufgaben mit Status im Detailfenster; Dokumenthinweise in Kanban, Kalender und Zeitachse
- Gemeinsame Suche in Titeln, Inhalten und eigenen Feldern; Filter und Gruppierung nach dem Eintragstyp „Aufgabe“ oder „Dokument“
- Seitliches Detailfenster beim Öffnen einer Aufgabe
- Wahlweise zentriertes Aufgaben-Popup oder Seitenpanel; Auswahl wird im Browser gespeichert
- Drag & Drop in der Baum-/Listenansicht zum Umsortieren und Ändern der Elternbeziehung, mit Einfügemarkierung und Schutz vor Kreisbeziehungen
- Eigene Felder vom Typ Text, Zahl, Datum, Auswahl und Ja/Nein
- Gespeicherte Ansichten für Baum/Tabelle, Kanban, Kalender und Zeitachse
- Pro Ansicht auswählbare sichtbare Felder und kombinierbare Filter, auch auf eigene Felder
- Gespeicherte Gruppierung nach Standard- oder eigenem Feld für Baum/Tabelle, Kanban und Zeitachse; Gruppen lassen sich einzeln ein- und ausklappen
- Gespeicherte Sortierung pro Ansicht (auf- oder absteigend, leere Werte am Ende); im Baum wird innerhalb jeder Ebene sortiert, ohne Sortierung gilt die manuelle Reihenfolge
- Pro Ansicht einstellbar: abgeschlossene Aufgaben ausblenden
- Datumsfilter mit relativen Werten wie „Heute“ oder „In 7 Tagen“, die sich täglich mitbewegen; die Ansicht „Anstehend“ zeigt damit offene Aufgaben, die überfällig oder in den nächsten 7 Tagen fällig sind
- Überfällige und heute fällige offene Aufgaben werden in allen Darstellungen farblich hervorgehoben
- Schnellanlage unter der Baumansicht: Neue Aufgaben und Dokumente übernehmen die Filterwerte der gewählten Konfiguration („ist“, „enthält“ sowie „vor“/„nach“ bei Datumsfeldern) und erscheinen so direkt in ihr. Angeboten werden nur Eintragstypen, die die Filter erfüllen können; weicht ein gespeicherter Eintrag doch ab, gibt es einen Hinweis
- Eigene Felder direkt in der Baum-/Tabellenansicht bearbeiten; Optionen von Auswahlfeldern einzeln anlegen, umbenennen und löschen
- Statuswechsel im Kanban per Drag & Drop; im Baum schließt ein Klick auf den Statuspunkt eine Aufgabe ab (oder öffnet sie wieder), ein Rechtsklick wählt einen beliebigen Status
- Aufgabenbalken in der Zeitachse verschieben oder über Griffe am Anfang und Ende verlängern/verkürzen; Tagesraster, Datumsvorschau und automatische Speicherung
- Zeitachse mit wählbaren Intervallen Woche, Monat und 3 Monate; die Auswahl bleibt im Browser gespeichert
- SunEditor für Überschriften, Listen, Tabellen und Bilder in Aufgabenbeschreibungen. Bilder lassen sich auch aus der Zwischenablage einfügen und werden im eingestellten Bilder-Ordner gespeichert (Standard: `.local/images`).

## Status

Die Status von Aufgaben sind frei konfigurierbar. Jeder Status gehört zu einer festen Kategorie: **Offen**, **In Arbeit** oder **Abgeschlossen**. Die App wertet nur die Kategorie aus: Abgeschlossene Aufgaben gelten als erledigt, werden nie als überfällig markiert und lassen sich pro Ansicht ausblenden. Neue Aufgaben erhalten den ersten offenen Status.

- Verwaltung unter **Eigene Felder** im Eintrag „Status“: Status umbenennen, Farbe wählen, hinzufügen, entfernen und per Ziehen (oder Pfeiltasten am Griff) sortieren, auch in eine andere Kategorie
- Im Kanban ist jeder Status eine Spalte. Ein Klick auf die Spaltenüberschrift öffnet ein Menü zum Umbenennen, für Farbe und Kategorie, zum Einfügen eines Status links oder rechts und zum Löschen; „＋ Status“ am Ende des Boards legt eine neue Spalte an
- Beim Löschen eines Status, der noch verwendet wird, wählt man den Status, der seine Aufgaben übernimmt. Gespeicherte Filter auf den gelöschten Status zeigen danach auf diesen Ersatz
- Jede Kategorie braucht mindestens einen Status
- Filter können einen einzelnen Status oder eine ganze Kategorie nennen („Alle offenen“, „Alle in Arbeit“, „Alle abgeschlossenen“). Solche Filter bleiben gültig, wenn später Status dazukommen

## Aufwände

Unter **Aufwände** in der Menüleiste werden Arbeitszeiten erfasst, getrennt von Aufgaben und Dokumenten. Ein Aufwand hat einen Tag, Beginn, Ende und eine Beschreibung.

- Wochenansicht von Montag bis Freitag mit dem Arbeitstag von 8 bis 18 Uhr; mit Pfeilen oder den Pfeiltasten wechselt man die Woche, „Heute“ springt zurück
- Ein Klick in einen freien Bereich erfasst die angeklickte halbe Stunde, Ziehen wählt einen beliebigen Zeitraum im 15-Minuten-Raster
- Einträge lassen sich per Drag & Drop auf eine andere Uhrzeit oder einen anderen Tag verschieben und an der Ober- oder Unterkante verlängern bzw. verkürzen; ein Klick öffnet sie zum Bearbeiten oder Löschen
- Summen pro Tag und für die Woche; überlappende Einträge stehen nebeneinander
- Liegen Einträge außerhalb von 8 bis 18 Uhr oder am Wochenende, wird die Ansicht automatisch erweitert. Das Wochenende lässt sich außerdem dauerhaft einblenden (im Browser gespeichert)
- Die angezeigte Woche steht in der Adresse (`/aufwaende?woche=2026-10-12`)

Dokumente verwenden denselben Editor und dieselben eigenen Felder wie Aufgaben. Ordner haben ausschließlich einen Titel und eine Beschreibung. Dokumente und Ordner haben keinen Status oder Aufgabenzeitraum und erscheinen im gemeinsamen Baum; Kanban, Kalender und Zeitachse zeigen Aufgaben. Ordner werden separat gezählt und können über den Button links neben „Aufgabe“ und „Dokument“ erstellt werden. Beim Löschen eines Baumeintrags werden auch seine untergeordneten Einträge gelöscht. Das Entfernen einer zusätzlichen Verknüpfung löscht hingegen weder das Dokument noch die Aufgabe.

Für die Baumansicht werden bei Treffern unterhalb eines gefilterten Elternknotens die nötigen Vorfahren zur Orientierung mit angezeigt. Die eigentlichen Filtertreffer bestimmt der Server.

## Aufbau

`Domain` enthält die Datenobjekte und die fachlichen Regeln, `Data` den EF Core Kontext und die Migrationen. `Services` übernimmt alle Änderungen, die Filterung und die Initialisierung. `Controllers` stellt die Oberfläche und JSON Endpunkte bereit und enthält selbst keine Logik.

`ItemKindRules` legt fest, was Ordner, Aufgaben und Dokumente haben dürfen und wie sie kombiniert werden. `FieldKeys` bildet die Feldschlüssel der Ansichten (`dueDate`, `field:{id}`). Optionen von Auswahlfeldern haben eine feste ID; gespeicherte Werte und Filter verweisen auf diese ID, sodass Umbenennen nichts weiter ändern muss. Filter, sichtbare Felder und Optionen liegen als JSON in je einer Spalte und werden in `JsonColumn` umgewandelt.

Services melden Regelverstöße mit `DomainValidationException` (400) oder `EntityNotFoundException` (404). `DomainExceptionFilter` macht daraus einheitlich ProblemDetails mit der Meldung in `detail`; der Bild-Upload ergänzt für SunEditor zusätzlich `errorMessage`. Die Razor Seiten liegen unter `Views/Workspace` und `Views/Settings`, die Oberfläche unter `wwwroot/js` und `wwwroot/css`. `StorageSettings` hält die Speicherorte, `StorageLocationService` verschiebt Datenbank und Bilder beim Ändern.

`WorkspaceItem` bildet den gemeinsamen Baum ab und unterscheidet mit `Kind` zwischen Ordner, Aufgabe und Dokument. `ItemLink` enthält zusätzliche Beziehungen zwischen Aufgaben und Dokumenten unabhängig von der Baumposition. `WorkspaceItemService` übernimmt Änderungen am Baum und den Inhalten sowie die Elternregeln für Ordner, `ItemLinkService` die Verknüpfungen. Die JSON-Endpunkte liegen unter `/api/items`; Verknüpfungen unter `/api/items/{id}/links`. Im Frontend sind die Berechnung der zugehörigen Einträge und deren Bearbeitung in `item-relations.js` und `item-links.js` gekapselt.

`TimeEntry` ist ein Aufwand; `TimeEntryService` prüft und speichert ihn (Ende nach Beginn, Beschreibung erforderlich, höchstens 500 Zeichen). Die Endpunkte liegen unter `/api/time-entries` (Abfrage mit `?from=…&to=…`), die Seite unter `/aufwaende`. Die Datums- und Layoutberechnung der Wochenansicht steckt in `time-week.js`, die Bedienung in `time-entries.js`.

SunEditor 3.3.3 liegt mitsamt Lizenz lokal unter `wwwroot/lib/suneditor`. Zum Starten ist deshalb weder npm noch eine CDN-Verbindung nötig. Für eingefügte Bilder gilt ein Limit von 5 MB pro Bild; unterstützt werden PNG, JPEG, GIF und WebP.

Neue Datenbankänderungen lassen sich mit dem lokalen EF Werkzeug erzeugen:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add MeineAenderung --output-dir Data/Migrations
```
