# Arbor – lokale Aufgabenverwaltung

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
- Seitliches Detailfenster beim Öffnen einer Aufgabe
- Eigene Felder vom Typ Text, Zahl, Datum, Auswahl und Ja/Nein
- Gespeicherte Ansichten für Baum/Tabelle, Kanban, Kalender und Zeitachse
- Pro Ansicht auswählbare sichtbare Felder und kombinierbare Filter, auch auf eigene Felder
- Statuswechsel im Kanban per Drag & Drop
- SunEditor für Überschriften, Listen, Tabellen und Bilder in Aufgabenbeschreibungen. Bilder lassen sich auch aus der Zwischenablage einfügen und werden lokal unter `.local/images` gespeichert.

Für die Baumansicht werden bei Treffern unterhalb eines gefilterten Elternknotens die nötigen Vorfahren zur Orientierung mit angezeigt. Die eigentlichen Filtertreffer bestimmt der Server.

## Aufbau

`Domain` enthält die Datenobjekte, `Data` den EF Core Kontext und die Migration. `Services` übernimmt Aufgabenänderungen, Filterung und die Initialisierung. `Controllers` stellt die Oberfläche und JSON Endpunkte bereit. Die Razor Seite liegt unter `Views/Workspace`, die Oberfläche unter `wwwroot/js` und `wwwroot/css`.

SunEditor 3.3.3 liegt mitsamt Lizenz lokal unter `wwwroot/lib/suneditor`. Zum Starten ist deshalb weder npm noch eine CDN-Verbindung nötig. Für eingefügte Bilder gilt ein Limit von 5 MB pro Bild; unterstützt werden PNG, JPEG, GIF und WebP.

Neue Datenbankänderungen lassen sich mit dem lokalen EF Werkzeug erzeugen:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add MeineAenderung --output-dir Data/Migrations
```
