# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

"Arbor" (repo/namespace `QuireClone`) is a local, single-user task and document manager: ASP.NET Core MVC on .NET 10, EF Core with SQLite, and a hand-written vanilla JS/CSS frontend (no build step, no npm, no frameworks; SunEditor is vendored under `wwwroot/lib`). There is no authentication. The UI text and the README are in German; code, identifiers and comments are in English. The README documents user-visible behavior in detail, so read it before changing features.

## Commands

```powershell
dotnet run                      # http://localhost:5222 (launch profile "http")
dotnet build
dotnet tool restore             # installs the pinned dotnet-ef from dotnet-tools.json
dotnet ef migrations add <Name> --output-dir Data/Migrations
```

There is no test project and no linter configured.

On startup `DatabaseInitializer` applies migrations automatically, switches SQLite to `journal_mode=DELETE` (single-file DB, no WAL), and seeds sample fields/views on an empty database. Runtime data lives in `.local/` (git-ignored): `arbor.db`, `images/`, and `storage.json` (user-chosen DB/image paths, read by `StorageSettings`). `AppDbContextFactory` resolves the DB path from the current directory, so run `dotnet ef` from the repo root.

## Architecture

**Layering.** `Domain` holds entities and business rules, `Data` the `AppDbContext` and migrations, `Services` all mutations, filtering and initialization, and `Controllers` are thin: they map routes to services and contain no logic. DTOs live in `Contracts/WorkspaceContracts.cs`.

**Errors.** Services throw `DomainValidationException` (→ 400) or `EntityNotFoundException` (→ 404). The global `DomainExceptionFilter` turns these into ProblemDetails with the message in `detail` (the image upload additionally sets `errorMessage` for SunEditor). The frontend `api()` helper in `wwwroot/js/api.js` surfaces `detail` (or model-validation `errors`) as the thrown error message, so user-facing German messages are written in the services.

**Antiforgery.** `AutoValidateAntiforgeryTokenAttribute` is global. The layout renders the token and `api()` sends it as the `RequestVerificationToken` header; any new fetch must go through `api()` or do the same.

**Projects are a strict partition.** `Project` is the flat top-level grouping. Items, statuses, custom fields and views carry a `ProjectId`; field values and links belong to their item's project. Services must verify that every reference (parent, status, field, link target, view field) belongs to the same project. Routes: collections and creation under `/api/projects/{projectId}/…`, existing objects addressed directly by id (`/api/items/{id}`, `/api/items/{id}/links`, …). Pages: `/projekte/{id}` (last project remembered in the `arbor-project` cookie), `/aufwaende` (time entries, global across projects), `/settings`. `ProjectService` + `ProjectDefaults` create projects with default statuses and views.

**One tree, three kinds.** `WorkspaceItem` models folders, tasks and documents in a single tree (`Kind`, `ParentId`, `SortOrder`). `ItemKindRules` is the source of truth for what each kind may have (only tasks have status/dates; folders have only title/description and may only sit at root or under folders; links connect exactly one task and one document). The frontend mirrors some rules in `item-relations.js` for UI purposes, but the server enforces them. `ItemLink` adds task↔document relations independent of tree position.

**Statuses.** Per-project rows (`TaskStatusDefinition`) with name, color and one of three fixed categories (Open/Active/Done). App logic only ever looks at the category. Filters may reference a single status id or a whole category via the `category:` prefix (`FieldKeys.StatusCategoryPrefix`).

**Views and filtering.** `ViewConfiguration` stores kind (Tree, Kanban, Calendar, Timeline), filters, visible fields, grouping and sorting. Field keys are built by `FieldKeys` (e.g. `dueDate`, `field:{id}`). `WorkspaceService.GetAsync` returns the whole project (all items, statuses, fields, views, links) plus `matchingItemIds`, which the server computes from the selected view's filters, including relative dates like `@today+7`. The client renders from that payload and adds ancestors for tree context itself. Select-field options have stable GUIDs; stored values and filters reference the id, never the label.

**JSON columns.** Filters, visible fields and select options are stored as JSON arrays in a single TEXT column via `JsonColumn.HasJsonConversion`. Changing the shape of those records means existing rows need converting.

**Migrations carry data conversions.** Several migrations rewrite existing data with raw SQL (e.g. `ConfigurableStatuses` maps old enum values and saved filters to status ids, `Projects` moves everything into "Mein Projekt"). Schema changes that affect stored values or JSON filter contents should migrate existing data the same way.

**Storage relocation.** `StorageSettings` (singleton) holds the DB and image paths; the DbContext options are built per request, so a changed location applies to the next request. `StorageLocationService` moves the DB and images when settings change, and only touches files the app created itself.

## Frontend conventions

- Each page loads one ES module entry from its Razor view: `app.js` (workspace), `time-entries.js`, `settings.js`, plus `sidebar.js` from `_Layout.cshtml`. Modules import each other with relative paths.
- Some imports carry a `?v=<tag>` query (e.g. `./api.js?v=problem-details`). The query is part of the module's identity: importing the same file with different specifiers creates separate module instances with separate state, so keep the specifier identical everywhere a module is imported. JS files are served with `Cache-Control: no-cache` (see `Program.cs`), so the tags aren't needed to avoid stale caches.
- `project.js` provides the current page's project id and helpers (`projectPath`, `projectKey` for per-project localStorage keys). Browser-only preferences use `localStorage` keys prefixed with `arbor-`.
