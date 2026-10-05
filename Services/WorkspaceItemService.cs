using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class WorkspaceItemService(AppDbContext db)
{
    public async Task<WorkspaceItem> SaveAsync(Guid? id, ItemSaveRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        if (!Enum.IsDefined(request.Kind)) throw new ArgumentException("Ungültiger Eintragstyp.");
        if (title.Length is 0 or > 300) throw new ArgumentException("Der Titel muss 1 bis 300 Zeichen enthalten.");
        if (request.StartDate > request.DueDate)
            throw new ArgumentException("Das Startdatum darf nicht nach dem Fälligkeitsdatum liegen.");
        if (!Enum.IsDefined(request.Status))
            throw new ArgumentException("Ungültiger Status.");
        if (request.Kind == WorkspaceItemKind.Folder &&
            (request.Fields.Count > 0 || request.StartDate is not null || request.DueDate is not null ||
             request.Status != Domain.TaskStatus.Todo))
            throw new ArgumentException("Ordner haben nur einen Titel und eine Beschreibung, keine eigenen Felder, Termine oder Aufgabenstatus.");

        WorkspaceItem item;
        if (id is null)
        {
            item = new WorkspaceItem
            {
                SortOrder = (await db.Items.Where(x => x.ParentId == request.ParentId)
                    .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1
            };
            db.Items.Add(item);
        }
        else
        {
            item = await db.Items.Include(x => x.FieldValues)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Eintrag nicht gefunden.");
            if (item.Kind != request.Kind)
                throw new ArgumentException("Der Eintragstyp kann nach dem Erstellen nicht geändert werden.");
            if (item.ParentId != request.ParentId)
                item.SortOrder = (await db.Items.Where(x => x.ParentId == request.ParentId)
                    .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1;
        }

        await ValidateParentAsync(item.Id, request.Kind, request.ParentId, cancellationToken);
        var definitions = await db.CustomFields.ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var (fieldId, value) in request.Fields)
        {
            if (!definitions.TryGetValue(fieldId, out var field))
                throw new ArgumentException("Ein benutzerdefiniertes Feld existiert nicht mehr.");
            ValidateValue(field, value);
        }

        item.Kind = request.Kind;
        item.Title = title;
        item.Description = request.Description.Trim();
        item.ParentId = request.ParentId;
        item.Status = request.Kind == WorkspaceItemKind.Task ? request.Status : Domain.TaskStatus.Todo;
        item.StartDate = request.Kind == WorkspaceItemKind.Task ? request.StartDate : null;
        item.DueDate = request.Kind == WorkspaceItemKind.Task ? request.DueDate : null;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var existing in item.FieldValues.ToList())
        {
            if (!request.Fields.TryGetValue(existing.FieldId, out var value) || string.IsNullOrWhiteSpace(value))
            {
                item.FieldValues.Remove(existing);
                db.ItemFieldValues.Remove(existing);
            }
            else existing.Value = value.Trim();
        }
        foreach (var (fieldId, value) in request.Fields.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
        {
            if (item.FieldValues.All(x => x.FieldId != fieldId))
                item.FieldValues.Add(new ItemFieldValue { ItemId = item.Id, FieldId = fieldId,
                    Value = value!.Trim() });
        }

        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task UpdateDatesAsync(Guid id, TaskDatesRequest request, CancellationToken cancellationToken)
    {
        if (request.StartDate is null && request.DueDate is null)
            throw new ArgumentException("Für den Zeitraum ist mindestens ein Datum erforderlich.");
        if (request.StartDate > request.DueDate)
            throw new ArgumentException("Das Startdatum darf nicht nach dem Fälligkeitsdatum liegen.");
        var item = await db.Items.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException("Eintrag nicht gefunden.");
        if (item.Kind != WorkspaceItemKind.Task)
            throw new ArgumentException("Nur Aufgaben haben einen Aufgabenzeitraum.");
        item.StartDate = request.StartDate;
        item.DueDate = request.DueDate;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFieldAsync(Guid id, Guid fieldId, string? value, CancellationToken cancellationToken)
    {
        var item = await db.Items.Include(x => x.FieldValues)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Eintrag nicht gefunden.");
        if (item.Kind == WorkspaceItemKind.Folder)
            throw new ArgumentException("Ordner haben keine eigenen Felder.");
        var field = await db.CustomFields.FindAsync([fieldId], cancellationToken)
            ?? throw new ArgumentException("Ein benutzerdefiniertes Feld existiert nicht mehr.");
        ValidateValue(field, value);

        var existing = item.FieldValues.FirstOrDefault(x => x.FieldId == fieldId);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing is null) return;
            item.FieldValues.Remove(existing);
            db.ItemFieldValues.Remove(existing);
        }
        else if (existing is null)
            item.FieldValues.Add(new ItemFieldValue { ItemId = item.Id, FieldId = fieldId, Value = value.Trim() });
        else existing.Value = value.Trim();
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveAsync(Guid id, TaskMoveRequest request, CancellationToken cancellationToken)
    {
        var item = await db.Items.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException("Eintrag nicht gefunden.");
        await ValidateParentAsync(id, item.Kind, request.ParentId, cancellationToken);
        var siblings = await db.Items.Where(x => x.ParentId == request.ParentId && x.Id != id)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var position = request.BeforeTaskId is Guid beforeId
            ? siblings.FindIndex(x => x.Id == beforeId) : siblings.Count;
        if (position < 0)
            throw new ArgumentException("Die Einfügeposition gehört nicht zur gewählten Ebene.");

        var oldParent = item.ParentId;
        if (oldParent != request.ParentId)
        {
            var oldSiblings = await db.Items.Where(x => x.ParentId == oldParent && x.Id != id)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
            for (var i = 0; i < oldSiblings.Count; i++) oldSiblings[i].SortOrder = i;
        }
        item.ParentId = request.ParentId;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        siblings.Insert(position, item);
        for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i;
        // EF saves the parent change and all sibling positions in one transaction.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Items.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException("Eintrag nicht gefunden.");
        db.Items.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateParentAsync(Guid itemId, WorkspaceItemKind kind, Guid? parentId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { itemId };
        var directParent = true;
        while (parentId is Guid current)
        {
            if (!visited.Add(current))
                throw new ArgumentException("Ein Eintrag kann nicht unter sich selbst eingeordnet werden.");
            var parent = await db.Items.Where(x => x.Id == current)
                .Select(x => new { x.ParentId, x.Kind }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException("Der übergeordnete Eintrag existiert nicht.");
            if (directParent && kind == WorkspaceItemKind.Folder && parent.Kind != WorkspaceItemKind.Folder)
                throw new ArgumentException("Ordner können nur auf der obersten Ebene oder unter anderen Ordnern liegen.");
            parentId = parent.ParentId;
            directParent = false;
        }
    }

    private static void ValidateValue(CustomFieldDefinition field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Length > 500) throw new ArgumentException($"{field.Name}: maximal 500 Zeichen.");
        var valid = field.Type switch
        {
            CustomFieldType.Number => decimal.TryParse(value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out _),
            CustomFieldType.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            CustomFieldType.Boolean => value is "true" or "false",
            CustomFieldType.Select => JsonSerializer.Deserialize<List<string>>(field.OptionsJson)!
                .Contains(value, StringComparer.Ordinal),
            _ => true
        };
        if (!valid) throw new ArgumentException($"Ungültiger Wert für {field.Name}.");
    }
}
