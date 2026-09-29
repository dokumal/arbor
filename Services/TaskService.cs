using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class TaskService(AppDbContext db)
{
    public async Task<TaskItem> SaveAsync(Guid? id, TaskSaveRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        if (title.Length is 0 or > 300) throw new ArgumentException("Der Titel muss 1 bis 300 Zeichen enthalten.");
        if (request.StartDate > request.DueDate)
            throw new ArgumentException("Das Startdatum darf nicht nach dem Fälligkeitsdatum liegen.");
        if (!Enum.IsDefined(request.Status))
            throw new ArgumentException("Ungültiger Status.");

        TaskItem task;
        if (id is null)
        {
            task = new TaskItem
            {
                SortOrder = (await db.Tasks.Where(x => x.ParentTaskId == request.ParentTaskId)
                    .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1
            };
            db.Tasks.Add(task);
        }
        else
        {
            task = await db.Tasks.Include(x => x.FieldValues)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Aufgabe nicht gefunden.");
            if (task.ParentTaskId != request.ParentTaskId)
                task.SortOrder = (await db.Tasks.Where(x => x.ParentTaskId == request.ParentTaskId)
                    .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1;
        }

        await ValidateParentAsync(task.Id, request.ParentTaskId, cancellationToken);
        var definitions = await db.CustomFields.ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var (fieldId, value) in request.Fields)
        {
            if (!definitions.TryGetValue(fieldId, out var field))
                throw new ArgumentException("Ein benutzerdefiniertes Feld existiert nicht mehr.");
            ValidateValue(field, value);
        }

        task.Title = title;
        task.Description = request.Description.Trim();
        task.ParentTaskId = request.ParentTaskId;
        task.Status = request.Status;
        task.StartDate = request.StartDate;
        task.DueDate = request.DueDate;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var existing in task.FieldValues.ToList())
        {
            if (!request.Fields.TryGetValue(existing.FieldId, out var value) || string.IsNullOrWhiteSpace(value))
            {
                task.FieldValues.Remove(existing);
                db.TaskFieldValues.Remove(existing);
            }
            else existing.Value = value.Trim();
        }
        foreach (var (fieldId, value) in request.Fields.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
        {
            if (task.FieldValues.All(x => x.FieldId != fieldId))
                task.FieldValues.Add(new TaskFieldValue { TaskId = task.Id, FieldId = fieldId,
                    Value = value!.Trim() });
        }

        await db.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task UpdateDatesAsync(Guid id, TaskDatesRequest request, CancellationToken cancellationToken)
    {
        if (request.StartDate is null && request.DueDate is null)
            throw new ArgumentException("Für den Zeitraum ist mindestens ein Datum erforderlich.");
        if (request.StartDate > request.DueDate)
            throw new ArgumentException("Das Startdatum darf nicht nach dem Fälligkeitsdatum liegen.");
        var task = await db.Tasks.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException("Aufgabe nicht gefunden.");
        task.StartDate = request.StartDate;
        task.DueDate = request.DueDate;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveAsync(Guid id, TaskMoveRequest request, CancellationToken cancellationToken)
    {
        var task = await db.Tasks.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException("Aufgabe nicht gefunden.");
        await ValidateParentAsync(id, request.ParentTaskId, cancellationToken);
        var siblings = await db.Tasks.Where(x => x.ParentTaskId == request.ParentTaskId && x.Id != id)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var position = request.BeforeTaskId is Guid beforeId
            ? siblings.FindIndex(x => x.Id == beforeId) : siblings.Count;
        if (position < 0)
            throw new ArgumentException("Die Einfügeposition gehört nicht zur gewählten Ebene.");

        var oldParent = task.ParentTaskId;
        if (oldParent != request.ParentTaskId)
        {
            var oldSiblings = await db.Tasks.Where(x => x.ParentTaskId == oldParent && x.Id != id)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
            for (var i = 0; i < oldSiblings.Count; i++) oldSiblings[i].SortOrder = i;
        }
        task.ParentTaskId = request.ParentTaskId;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        siblings.Insert(position, task);
        for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i;
        // EF saves the parent change and all sibling positions in one transaction.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await db.Tasks.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException("Aufgabe nicht gefunden.");
        db.Tasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateParentAsync(Guid taskId, Guid? parentId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { taskId };
        while (parentId is Guid current)
        {
            if (!visited.Add(current))
                throw new ArgumentException("Eine Aufgabe kann nicht unter sich selbst eingeordnet werden.");
            parentId = await db.Tasks.Where(x => x.Id == current)
                .Select(x => x.ParentTaskId).FirstOrDefaultAsync(cancellationToken);
            if (parentId is null && !await db.Tasks.AnyAsync(x => x.Id == current, cancellationToken))
                throw new ArgumentException("Die übergeordnete Aufgabe existiert nicht.");
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
