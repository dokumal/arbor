# Arbor – Aufgaben und Dokumente

Eine ASP.NET Core MVC Anwendung mit Entity Framework Core und SQLite. Die Oberfläche verwendet nur eigenes CSS und JavaScript. Es gibt derzeit keine Anmeldung; die App ist für den lokalen Betrieb gedacht.

## Starten

Voraussetzung ist nur das .NET 10 SDK. Zum Starten reicht:

```powershell
dotnet run
```

Danach `http://localhost:5222` öffnen. Beim ersten Start legt die App die Datenbank `.local/arbor.db` an, führt die EF Core Migration aus und legt das erste Projekt mit Beispiel-Feldern und -Ansichten an. Bilder aus Beschreibungen landen in `.local/images`. Der Ordner `.local` ist von Git ausgeschlossen.

## Speicherorte

Über **Einstellungen** in der Seitenleiste lassen sich der Pfad der Datenbank-Datei und der Bilder-Ordner ändern. Pfade können absolut oder relativ zum Programmordner sein; Umgebungsvariablen wie `%USERPROFILE%` werden aufgelöst. Die Auswahl wird in `.local/storage.json` gespeichert, nicht in der Datenbank selbst.

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
- Tiptap-Editor für Beschreibungen und Dokumentinhalte mit Text, Überschriften, Listen, Tabellen und Bildern. Tippt man `/`, öffnet sich ein Befehlsmenü (Pfeiltasten und Enter, Escape schließt es); innerhalb einer Tabelle bietet es zusätzlich Befehle zum Einfügen und Löschen von Zeilen und Spalten. Dieselben Befehle zeigt eine kleine Leiste über der Tabelle, solange der Cursor darin steht; Tab springt zur nächsten Zelle und hängt in der letzten Zelle eine neue Zeile an. Markdown-Kürzel wie `# `, `- `, `1. ` oder `[ ] ` funktionieren ebenfalls. Bilder lassen sich über `/Bild`, aus der Zwischenablage oder per Drag-and-drop einfügen und werden im eingestellten Bilder-Ordner gespeichert (Standard: `.local/images`).
- Markiert man im Editor bereits geschriebenen Text, erscheint darüber eine Formatierungsleiste (Tiptaps Bubble Menu): Fett, Kursiv, Unterstrichen, Durchgestrichen, Text, Überschrift 1–3, Aufzählung, nummerierte Liste und Checkliste. Die aktuell gesetzten Formate sind hervorgehoben; Blockformate wandeln die markierten Absätze um, auch zwischen Liste und Überschrift. Fett, Kursiv, Unterstrichen und Durchgestrichen gehen weiterhin auch per Strg+B, Strg+I, Strg+U und Strg+Umschalt+S
- Checklisten im Editor über `/Checkliste` oder das Kürzel `[ ] `; Punkte lassen sich einrücken (Tab) und per Klick abhaken, abgehakte Punkte erscheinen durchgestrichen. Der Fortschritt aller Checklisten eines Eintrags steht dezent in Baum und Kanban (z. B. „☑ 3/7“, grün, sobald alles erledigt ist) sowie in Kalender und Zeitachse. Abgehakte Kästchen werden wie der übrige Inhalt mit „Speichern“ übernommen
- Gelöschte Einträge landen im Papierkorb und lassen sich von dort wiederherstellen (siehe unten)

## Papierkorb

Löschen verschiebt einen Ordner, eine Aufgabe oder ein Dokument mitsamt allen untergeordneten Einträgen in den Papierkorb. Eine Rückfrage gibt es dafür nicht; die Meldung danach bietet „Rückgängig“ an. Einträge im Papierkorb erscheinen in keiner Ansicht, keinem Filter und keiner Suche, behalten aber ihre Feldwerte und Verknüpfungen.

- Der **Papierkorb** in der Seitenleiste (`/papierkorb`) zeigt die gelöschten Einträge aller Projekte, nach Projekt gruppiert und mit dem ursprünglichen Ort, dem Zeitpunkt und der Zahl der mitgelöschten Einträge
- „Wiederherstellen“ holt den Eintrag mit allem zurück, was mit ihm gelöscht wurde, an seine frühere Stelle. Liegt der ursprüngliche übergeordnete Eintrag selbst im Papierkorb, kommt er unter den nächsten noch vorhandenen übergeordneten Eintrag (oder auf die oberste Ebene); die Meldung nennt den neuen Ort
- Wurde ein untergeordneter Eintrag schon vorher einzeln gelöscht, bleibt er ein eigener Eintrag im Papierkorb und wird nicht automatisch mit wiederhergestellt
- „Endgültig löschen“ entfernt einen Eintrag samt Inhalt unwiderruflich; einzeln gelöschte Einträge darunter bleiben im Papierkorb. „Papierkorb leeren“ entfernt alles
- Wird ein Projekt gelöscht, verschwinden auch seine Einträge im Papierkorb

## Projekte

Aufgaben, Dokumente und Ordner gehören jeweils zu genau einem Projekt. Projekte sind nicht verschachtelt und streng voneinander getrennt: Jedes Projekt hat eigene Status, eigene Felder und eigene gespeicherte Ansichten. Übergeordnete Einträge, Verknüpfungen, Status und Feldwerte können nur innerhalb desselben Projekts verwendet werden; Status- und Feldnamen müssen nur innerhalb eines Projekts eindeutig sein.

- Die Projekte stehen in der Seitenleiste. „＋“ legt ein neues Projekt an; es startet mit den Status „Offen“, „In Arbeit“ und „Erledigt“ und den üblichen Ansichten, aber ohne eigene Felder
- „⋯“ neben einem Projekt öffnet das Bearbeiten: umbenennen oder löschen. Beim Löschen werden alle Einträge, Status, Felder und Ansichten des Projekts entfernt. Das letzte Projekt kann nicht gelöscht werden
- Ein Projekt hat die Adresse `/projekte/{id}`. Die Startseite öffnet das zuletzt besuchte Projekt
- Die zuletzt gewählte Ansicht merkt sich der Browser pro Projekt
- Daten aus der Zeit vor den Projekten liegen im Projekt „Mein Projekt“

## Seitenleiste

Die Navigation liegt in einer Seitenleiste links. Das Menü-Symbol oben links blendet sie ein und aus; geöffnet liegt sie über der Seite und schließt sich mit Escape oder einem Klick daneben. Mit der Stecknadel lässt sie sich fixieren: Sie steht dann dauerhaft neben dem Inhalt, auch auf den anderen Seiten (im Browser gespeichert). Auf schmalen Bildschirmen liegt sie immer über der Seite.

## Status

Die Status von Aufgaben sind frei konfigurierbar. Jeder Status gehört zu einer festen Kategorie: **Offen**, **In Arbeit** oder **Abgeschlossen**. Die App wertet nur die Kategorie aus: Abgeschlossene Aufgaben gelten als erledigt, werden nie als überfällig markiert und lassen sich pro Ansicht ausblenden. Neue Aufgaben erhalten den ersten offenen Status.

- Verwaltung unter **Eigene Felder** im Eintrag „Status“: Status umbenennen, Farbe wählen, hinzufügen, entfernen und per Ziehen (oder Pfeiltasten am Griff) sortieren, auch in eine andere Kategorie
- Im Kanban ist jeder Status eine Spalte. Ein Klick auf die Spaltenüberschrift öffnet ein Menü zum Umbenennen, für Farbe und Kategorie, zum Einfügen eines Status links oder rechts und zum Löschen; „＋ Status“ am Ende des Boards legt eine neue Spalte an
- Beim Löschen eines Status, der noch verwendet wird, wählt man den Status, der seine Aufgaben übernimmt. Gespeicherte Filter auf den gelöschten Status zeigen danach auf diesen Ersatz
- Jede Kategorie braucht mindestens einen Status
- Filter können einen einzelnen Status oder eine ganze Kategorie nennen („Alle offenen“, „Alle in Arbeit“, „Alle abgeschlossenen“). Solche Filter bleiben gültig, wenn später Status dazukommen

## Aufwände

Unter **Aufwände** in der Seitenleiste werden Arbeitszeiten erfasst, getrennt von Aufgaben und Dokumenten und für alle Projekte gemeinsam. Ein Aufwand hat einen Tag, Beginn, Ende und eine Beschreibung.

- Wochenansicht von Montag bis Freitag mit dem Arbeitstag von 8 bis 18 Uhr; mit Pfeilen oder den Pfeiltasten wechselt man die Woche, „Heute“ springt zurück
- Ein Klick in einen freien Bereich erfasst die angeklickte halbe Stunde, Ziehen wählt einen beliebigen Zeitraum im 15-Minuten-Raster
- Einträge lassen sich per Drag & Drop auf eine andere Uhrzeit oder einen anderen Tag verschieben und an der Ober- oder Unterkante verlängern bzw. verkürzen; ein Klick öffnet sie zum Bearbeiten oder Löschen
- Summen pro Tag und für die Woche; überlappende Einträge stehen nebeneinander
- Liegen Einträge außerhalb von 8 bis 18 Uhr oder am Wochenende, wird die Ansicht automatisch erweitert. Das Wochenende lässt sich außerdem dauerhaft einblenden (im Browser gespeichert)
- Die angezeigte Woche steht in der Adresse (`/aufwaende?woche=2026-10-12`)

Dokumente verwenden denselben Editor und dieselben eigenen Felder wie Aufgaben. Ordner haben ausschließlich einen Titel und eine Beschreibung. Dokumente und Ordner haben keinen Status oder Aufgabenzeitraum und erscheinen im gemeinsamen Baum; Kanban, Kalender und Zeitachse zeigen Aufgaben. Ordner werden separat gezählt und können über den Button links neben „Aufgabe“ und „Dokument“ erstellt werden. Beim Löschen eines Baumeintrags kommen auch seine untergeordneten Einträge in den Papierkorb. Das Entfernen einer zusätzlichen Verknüpfung löscht hingegen weder das Dokument noch die Aufgabe.

Für die Baumansicht werden bei Treffern unterhalb eines gefilterten Elternknotens die nötigen Vorfahren zur Orientierung mit angezeigt. Die eigentlichen Filtertreffer bestimmt der Server.

## Aufbau

`Domain` enthält die Datenobjekte und die fachlichen Regeln, `Data` den EF Core Kontext und die Migrationen. `Services` übernimmt alle Änderungen, die Filterung und die Initialisierung. `Controllers` stellt die Oberfläche und JSON Endpunkte bereit und enthält selbst keine Logik.

`ItemKindRules` legt fest, was Ordner, Aufgaben und Dokumente haben dürfen und wie sie kombiniert werden. `FieldKeys` bildet die Feldschlüssel der Ansichten (`dueDate`, `field:{id}`). Optionen von Auswahlfeldern haben eine feste ID; gespeicherte Werte und Filter verweisen auf diese ID, sodass Umbenennen nichts weiter ändern muss. Filter, sichtbare Felder und Optionen liegen als JSON in je einer Spalte und werden in `JsonColumn` umgewandelt.

Services melden Regelverstöße mit `DomainValidationException` (400) oder `EntityNotFoundException` (404). `DomainExceptionFilter` macht daraus einheitlich ProblemDetails mit der Meldung in `detail`. Die Razor Seiten liegen unter `Views/Workspace` und `Views/Settings`, die Oberfläche unter `wwwroot/js` und `wwwroot/css`. `StorageSettings` hält die Speicherorte, `StorageLocationService` verschiebt Datenbank und Bilder beim Ändern.

`Project` ist die oberste Gruppierung. Einträge, Status, eigene Felder und Ansichten tragen eine `ProjectId`; Feldwerte und Verknüpfungen hängen an Einträgen und gehören damit ebenfalls zu deren Projekt. `ProjectService` legt Projekte mit ihren Standard-Status und -Ansichten (`ProjectDefaults`) an und löscht sie samt Inhalt. Die Services prüfen, dass alles, worauf ein Eintrag oder eine Ansicht verweist, aus demselben Projekt stammt. Projekte werden unter `/api/projects` verwaltet; Listen und neue Einträge, Felder, Ansichten und Status liegen unter `/api/projects/{projectId}/…`, bestehende Objekte werden direkt über ihre ID angesprochen. Die Seitenleiste steuert `sidebar.js`, die Projekt-ID der Seite liefert `project.js`.

`WorkspaceItem` bildet den gemeinsamen Baum ab und unterscheidet mit `Kind` zwischen Ordner, Aufgabe und Dokument. `ItemLink` enthält zusätzliche Beziehungen zwischen Aufgaben und Dokumenten unabhängig von der Baumposition. `WorkspaceItemService` übernimmt Änderungen am Baum und den Inhalten sowie die Elternregeln für Ordner, `ItemLinkService` die Verknüpfungen. Neue Einträge entstehen unter `/api/projects/{projectId}/items`, bestehende werden unter `/api/items/{id}` geändert; Verknüpfungen liegen unter `/api/items/{id}/links`. Im Frontend sind die Berechnung der zugehörigen Einträge und deren Bearbeitung in `item-relations.js` und `item-links.js` gekapselt.

`TimeEntry` ist ein Aufwand; `TimeEntryService` prüft und speichert ihn (Ende nach Beginn, Beschreibung erforderlich, höchstens 500 Zeichen). Die Endpunkte liegen unter `/api/time-entries` (Abfrage mit `?from=…&to=…`), die Seite unter `/aufwaende`. Die Datums- und Layoutberechnung der Wochenansicht steckt in `time-week.js`, die Bedienung in `time-entries.js`.

Der Papierkorb ist ein Soft-Delete: `DeletedAt` markiert einen gelöschten Eintrag, und alle mitgelöschten Einträge tragen in `DeletedWithId` die ID des Eintrags, der gelöscht wurde. Ein globaler Query-Filter im `AppDbContext` blendet solche Einträge samt ihren Feldwerten und Verknüpfungen in allen Abfragen aus; nur `TrashService` und Operationen auf ganzen Projekten, Status oder Auswahloptionen umgehen ihn mit `IgnoreQueryFilters`. `TrashService` verschiebt in den Papierkorb, listet, stellt wieder her und löscht endgültig; die Endpunkte liegen unter `/api/trash`, die Seite unter `/papierkorb` mit `trash.js`. Den Fortschritt von Checklisten berechnet `checklistProgress` in `render.js` aus dem gespeicherten HTML.

Tiptap 3.32.0 liegt als ein einzelnes, fertig gebündeltes ES-Modul mitsamt Lizenzen unter `wwwroot/lib/tiptap`. Zum Starten ist deshalb weder npm noch eine CDN-Verbindung nötig. Nur wer Tiptap aktualisieren oder weitere Erweiterungen aufnehmen will, braucht Node.js: Versionen in `tools/tiptap/package.json` anpassen, Exporte in `tools/tiptap/entry.js` ergänzen und im Ordner `tools/tiptap` `npm install` und `npm run build` ausführen. Der Editor selbst steckt in `description-editor.js`, das Befehlsmenü in `slash-commands.js`, die Formatierungsleiste für markierten Text in `format-menu.js`. Für eingefügte Bilder gilt ein Limit von 5 MB pro Bild; unterstützt werden PNG, JPEG, GIF und WebP.

Neue Datenbankänderungen lassen sich mit dem lokalen EF Werkzeug erzeugen:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add MeineAenderung --output-dir Data/Migrations
```
