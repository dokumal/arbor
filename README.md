# Arbor – Aufgaben und Dokumente

Eine ASP.NET Core MVC Anwendung mit Entity Framework Core und PostgreSQL. Die Oberfläche verwendet nur eigenes CSS und JavaScript. Es gibt derzeit keine Anmeldung; die App ist für den lokalen Betrieb gedacht.

## Starten

Voraussetzungen: .NET 10 SDK und eine lokale PostgreSQL-Installation mit `initdb` und `pg_ctl`. Auf Windows wird ein installiertes PostgreSQL unter `C:\Program Files\PostgreSQL` automatisch gefunden. Zum Starten reicht:

```powershell
dotnet run
```

Danach `http://localhost:5222` öffnen. Beim ersten Start legt die App unter `.local/postgres` eine eigene Datenbank an. Sie startet PostgreSQL bei jedem App-Start selbst und stoppt die von ihr gestartete Instanz beim Beenden. Die Daten bleiben in `.local/postgres/data` erhalten. Die EF Core Migration wird automatisch ausgeführt. Die Datenbank lauscht ausschließlich auf `127.0.0.1`.

Falls die PostgreSQL-Werkzeuge an einem anderen Ort liegen, vor `dotnet run` den bin-Ordner angeben:

```powershell
$env:POSTGRES_BIN = 'C:\Pfad\zu\PostgreSQL\bin'
dotnet run
```

Soll stattdessen eine bereits laufende PostgreSQL-Instanz verwendet werden, die Verbindungszeichenfolge überschreiben:

```powershell
$env:ConnectionStrings__Postgres = 'Host=localhost;Port=5432;Database=quireclone;Username=MEIN_BENUTZER;Password=MEIN_PASSWORT'
dotnet run
```

Die Zugangsdaten der automatisch angelegten Datenbank werden lokal unter `.local/postgres/settings.json` gespeichert. Dieser Ordner ist von Git ausgeschlossen. Für eine bestehende Instanz eigene Zugangsdaten verwenden.

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
- Pro Ansicht einstellbar: erledigte Aufgaben ausblenden
- Datumsfilter mit relativen Werten wie „Heute“ oder „In 7 Tagen“, die sich täglich mitbewegen; die Ansicht „Anstehend“ zeigt damit offene Aufgaben, die überfällig oder in den nächsten 7 Tagen fällig sind
- Überfällige und heute fällige offene Aufgaben werden in allen Darstellungen farblich hervorgehoben
- Eigene Felder direkt in der Baum-/Tabellenansicht bearbeiten; Optionen von Auswahlfeldern einzeln anlegen, umbenennen und löschen
- Statuswechsel im Kanban per Drag & Drop
- Aufgabenbalken in der Zeitachse verschieben oder über Griffe am Anfang und Ende verlängern/verkürzen; Tagesraster, Datumsvorschau und automatische Speicherung
- Zeitachse mit wählbaren Intervallen Woche, Monat und 3 Monate; die Auswahl bleibt im Browser gespeichert
- SunEditor für Überschriften, Listen, Tabellen und Bilder in Aufgabenbeschreibungen. Bilder lassen sich auch aus der Zwischenablage einfügen und werden lokal unter `.local/images` gespeichert.

Dokumente verwenden denselben Editor und dieselben eigenen Felder wie Aufgaben. Ordner haben ausschließlich einen Titel und eine Beschreibung. Dokumente und Ordner haben keinen Status oder Aufgabenzeitraum und erscheinen im gemeinsamen Baum; Kanban, Kalender und Zeitachse zeigen Aufgaben. Ordner werden separat gezählt und können über den Button links neben „Aufgabe“ und „Dokument“ erstellt werden. Beim Löschen eines Baumeintrags werden auch seine untergeordneten Einträge gelöscht. Das Entfernen einer zusätzlichen Verknüpfung löscht hingegen weder das Dokument noch die Aufgabe.

Für die Baumansicht werden bei Treffern unterhalb eines gefilterten Elternknotens die nötigen Vorfahren zur Orientierung mit angezeigt. Die eigentlichen Filtertreffer bestimmt der Server.

## Aufbau

`Domain` enthält die Datenobjekte, `Data` den EF Core Kontext und die Migration. `Services` übernimmt Aufgabenänderungen, Filterung und die Initialisierung. `Controllers` stellt die Oberfläche und JSON Endpunkte bereit. Die Razor Seite liegt unter `Views/Workspace`, die Oberfläche unter `wwwroot/js` und `wwwroot/css`.

`WorkspaceItem` bildet den gemeinsamen Baum ab und unterscheidet mit `Kind` zwischen Ordner, Aufgabe und Dokument. `ItemLink` enthält zusätzliche Beziehungen zwischen Aufgaben und Dokumenten unabhängig von der Baumposition. `WorkspaceItemService` übernimmt Änderungen am Baum und den Inhalten sowie die Elternregeln für Ordner, `ItemLinkService` die Verknüpfungen. Die JSON-Endpunkte liegen unter `/api/items`; Verknüpfungen unter `/api/items/{id}/links`. Im Frontend sind die Berechnung der zugehörigen Einträge und deren Bearbeitung in `item-relations.js` und `item-links.js` gekapselt. Die vorhandenen Tabellen und IDs bleiben bei der Migration erhalten; bestehende Einträge werden als Aufgaben eingestuft. Der zusätzliche Typ „Ordner“ wird in der vorhandenen Typ-Spalte gespeichert und benötigt keine weitere Datenbankmigration.

SunEditor 3.3.3 liegt mitsamt Lizenz lokal unter `wwwroot/lib/suneditor`. Zum Starten ist deshalb weder npm noch eine CDN-Verbindung nötig. Für eingefügte Bilder gilt ein Limit von 5 MB pro Bild; unterstützt werden PNG, JPEG, GIF und WebP.

Neue Datenbankänderungen lassen sich mit dem lokalen EF Werkzeug erzeugen:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add MeineAenderung --output-dir Data/Migrations
```
