using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class WorkspaceItemService(AppDbContext db, StatusService statuses, ProjectService projects)
{
    public async Task<WorkspaceItem> CreateAsync(Guid projectId, ItemSaveRequest request, CancellationToken cancellationToken)
    {
        await projects.EnsureExistsAsync(projectId, cancellationToken);
        return await SaveAsync(null, projectId, request, cancellationToken);
    }

    public async Task UpdateAsync(Guid id, ItemSaveRequest request, CancellationToken cancellationToken)
    {
        var projectId = await db.Items.Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Eintrag nicht gefunden.");
        await SaveAsync(id, projectId, request, cancellationToken);
    }

    private async Task<WorkspaceItem> SaveAsync(Guid? id, Guid projectId, ItemSaveRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        if (!Enum.IsDefined(request.Kind)) throw new DomainValidationException("Ungültiger Eintragstyp.");
        if (title.Length is 0 or > 300) throw new DomainValidationException("Der Titel muss 1 bis 300 Zeichen enthalten.");
        if (request.StartDate > request.DueDate)
            throw new DomainValidationException("Das Startdatum darf nicht nach dem Fälligkeitsdatum liegen.");
        if (!ItemKindRules.HasSchedule(request.Kind) &&
            (request.StartDate is not null || request.DueDate is not null || request.StatusId is not null))
            throw new DomainValidationException("Nur Aufgaben haben Termine und einen Status.");
        var statusId = ItemKindRules.HasSchedule(request.Kind)
            ? await ValidStatusIdAsync(projectId, request.StatusId, cancellationToken) : (Guid?)null;
        if (!ItemKindRules.HasCustomFields(request.Kind) && request.Fields.Count > 0)
            throw new DomainValidationException("Ordner haben keine eigenen Felder.");

        WorkspaceItem item;
        if (id is null)
        {
            item = new WorkspaceItem { ProjectId = projectId, SortOrder = await NextSortOrderAsync(projectId, request.ParentId, cancellationToken) };
            db.Items.Add(item);
        }
        else
        {
            item = await db.Items.Include(x => x.FieldValues)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new EntityNotFoundException("Eintrag nicht gefunden.");
            if (item.Kind != request.Kind)
                throw new DomainValidationException("Der Eintragstyp kann nach dem Erstellen nicht geändert werden.");
            if (item.ParentId != request.ParentId)
                item.SortOrder = await NextSortOrderAsync(projectId, request.ParentId, cancellationToken);
        }

        await ValidateParentAsync(projectId, item.Id, request.Kind, request.ParentId, cancellationToken);
        var definitions = await db.CustomFields.Where(x => x.ProjectId == projectId).ToDictionaryAsync(x => x.Id, cancellationToken);
        var values = new Dictionary<Guid, string>();
        foreach (var (fieldId, value) in request.Fields)
        {
            if (!definitions.TryGetValue(fieldId, out var field))
                throw new DomainValidationException("Ein benutzerdefiniertes Feld existiert nicht mehr.");
            if (NormalizeValue(field, value) is { } normalized) values[fieldId] = normalized;
        }

        item.Kind = request.Kind;
        item.Title = title;
        item.Description = request.Description.Trim();
        item.ParentId = request.ParentId;
        item.StatusId = statusId;
        item.StartDate = request.StartDate;
        item.DueDate = request.DueDate;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var existing in item.FieldValues.ToList())
        {
            if (values.TryGetValue(existing.FieldId, out var value)) existing.Value = value;
            else
            {
                item.FieldValues.Remove(existing);
                db.ItemFieldValues.Remove(existing);
            }
        }
        foreach (var (fieldId, value) in values)
        {
            if (item.FieldValues.All(x => x.FieldId != fieldId))
                item.FieldValues.Add(new ItemFieldValue { ItemId = item.Id, FieldId = fieldId, Value = value });
        }

        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task UpdateDatesAsync(Guid id, TaskDatesRequest request, CancellationToken cancellationToken)
    {
        if (request.StartDate is null && request.DueDate is null)
            throw new DomainValidationException("Für den Zeitraum ist mindestens ein Datum erforderlich.");
        if (request.StartDate > request.DueDate)
            throw new DomainValidationException("Das Startdatum darf nicht nach dem Fälligkeitsdatum liegen.");
        var item = await FindAsync(id, cancellationToken);
        if (!ItemKindRules.HasSchedule(item.Kind))
            throw new DomainValidationException("Nur Aufgaben haben einen Aufgabenzeitraum.");
        item.StartDate = request.StartDate;
        item.DueDate = request.DueDate;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(Guid id, Guid statusId, CancellationToken cancellationToken)
    {
        var item = await FindAsync(id, cancellationToken);
        if (!ItemKindRules.HasSchedule(item.Kind))
            throw new DomainValidationException("Nur Aufgaben haben einen Status.");
        item.StatusId = await ValidStatusIdAsync(item.ProjectId, statusId, cancellationToken);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFieldAsync(Guid id, Guid fieldId, string? value, CancellationToken cancellationToken)
    {
        var item = await db.Items.Include(x => x.FieldValues)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("Eintrag nicht gefunden.");
        if (!ItemKindRules.HasCustomFields(item.Kind))
            throw new DomainValidationException("Ordner haben keine eigenen Felder.");
        var field = await db.CustomFields.FirstOrDefaultAsync(x => x.Id == fieldId && x.ProjectId == item.ProjectId, cancellationToken)
            ?? throw new DomainValidationException("Ein benutzerdefiniertes Feld existiert nicht mehr.");
        var normalized = NormalizeValue(field, value);

        var existing = item.FieldValues.FirstOrDefault(x => x.FieldId == fieldId);
        if (normalized is null)
        {
            if (existing is null) return;
            item.FieldValues.Remove(existing);
            db.ItemFieldValues.Remove(existing);
        }
        else if (existing is null)
            item.FieldValues.Add(new ItemFieldValue { ItemId = item.Id, FieldId = fieldId, Value = normalized });
        else existing.Value = normalized;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveAsync(Guid id, TaskMoveRequest request, CancellationToken cancellationToken)
    {
        var item = await FindAsync(id, cancellationToken);
        await ValidateParentAsync(item.ProjectId, id, item.Kind, request.ParentId, cancellationToken);
        var siblings = await db.Items.Where(x => x.ProjectId == item.ProjectId && x.ParentId == request.ParentId && x.Id != id)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var position = request.BeforeTaskId is Guid beforeId
            ? siblings.FindIndex(x => x.Id == beforeId) : siblings.Count;
        if (position < 0)
            throw new DomainValidationException("Die Einfügeposition gehört nicht zur gewählten Ebene.");

        var oldParent = item.ParentId;
        if (oldParent != request.ParentId)
        {
            var oldSiblings = await db.Items.Where(x => x.ProjectId == item.ProjectId && x.ParentId == oldParent && x.Id != id)
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
        db.Items.Remove(await FindAsync(id, cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<WorkspaceItem> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Items.FindAsync([id], cancellationToken) ?? throw new EntityNotFoundException("Eintrag nicht gefunden.");

    // A task without a status gets the default one; a given status must belong to the same project.
    private async Task<Guid> ValidStatusIdAsync(Guid projectId, Guid? statusId, CancellationToken cancellationToken)
    {
        if (statusId is null) return await statuses.DefaultIdAsync(projectId, cancellationToken);
        if (!await db.Statuses.AnyAsync(x => x.Id == statusId && x.ProjectId == projectId, cancellationToken))
            throw new DomainValidationException("Der Status existiert nicht mehr. Bitte lade die Seite neu.");
        return statusId.Value;
    }

    private async Task<int> NextSortOrderAsync(Guid projectId, Guid? parentId, CancellationToken cancellationToken) =>
        (await db.Items.Where(x => x.ProjectId == projectId && x.ParentId == parentId).MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1;

    // The parent must belong to the same project; its ancestors then do as well.
    private async Task ValidateParentAsync(Guid projectId, Guid itemId, WorkspaceItemKind kind, Guid? parentId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { itemId };
        var directParent = true;
        while (parentId is Guid current)
        {
            if (!visited.Add(current))
                throw new DomainValidationException("Ein Eintrag kann nicht unter sich selbst eingeordnet werden.");
            var parent = await db.Items.Where(x => x.Id == current)
                .Select(x => new { x.ParentId, x.Kind, x.ProjectId }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new DomainValidationException("Der übergeordnete Eintrag existiert nicht.");
            if (directParent && parent.ProjectId != projectId)
                throw new DomainValidationException("Der übergeordnete Eintrag gehört zu einem anderen Projekt.");
            if (directParent && !ItemKindRules.CanContain(parent.Kind, kind))
                throw new DomainValidationException("Ordner können nur auf der obersten Ebene oder unter anderen Ordnern liegen.");
            parentId = parent.ParentId;
            directParent = false;
        }
    }

    // Returns the value to store, or null when the field is left empty.
    private static string? NormalizeValue(CustomFieldDefinition field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 500) throw new DomainValidationException($"{field.Name}: maximal 500 Zeichen.");
        if (field.Type == CustomFieldType.Select)
        {
            // Select values are option ids; store them in one canonical format.
            if (!Guid.TryParse(value, out var optionId) || field.Options.All(x => x.Id != optionId))
                throw new DomainValidationException($"Ungültiger Wert für {field.Name}.");
            return optionId.ToString();
        }
        var valid = field.Type switch
        {
            CustomFieldType.Number => decimal.TryParse(value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out _),
            CustomFieldType.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            CustomFieldType.Boolean => value is "true" or "false",
            _ => true
        };
        if (!valid) throw new DomainValidationException($"Ungültiger Wert für {field.Name}.");
        return value;
    }
}
