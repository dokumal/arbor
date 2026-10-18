using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class CustomFieldService(AppDbContext db, ProjectService projects)
{
    public async Task<Guid> CreateAsync(Guid projectId, CustomFieldSaveRequest request, CancellationToken cancellationToken)
    {
        await projects.EnsureExistsAsync(projectId, cancellationToken);
        var name = request.Name.Trim();
        if (name.Length == 0) throw new DomainValidationException("Bitte einen Feldnamen eingeben.");
        if (!Enum.IsDefined(request.Type)) throw new DomainValidationException("Ungültiger Feldtyp.");
        if (await db.CustomFields.AnyAsync(x => x.ProjectId == projectId && x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new DomainValidationException("Ein Feld mit diesem Namen existiert bereits.");
        var options = request.Type == CustomFieldType.Select
            ? request.Options.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList() : [];
        if (request.Type == CustomFieldType.Select && options.Count == 0)
            throw new DomainValidationException("Für ein Auswahlfeld sind Optionen erforderlich.");
        if (options.Any(x => x.Length > 500))
            throw new DomainValidationException("Optionen dürfen maximal 500 Zeichen lang sein.");

        var field = new CustomFieldDefinition
        {
            ProjectId = projectId, Name = name, Type = request.Type,
            Options = options.Select(x => new SelectOption(Guid.NewGuid(), x)).ToList(),
            SortOrder = (await db.CustomFields.Where(x => x.ProjectId == projectId)
                .MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1
        };
        db.CustomFields.Add(field);
        await db.SaveChangesAsync(cancellationToken);
        return field.Id;
    }

    // Renamed options keep their id, so stored values and saved filters stay valid; removed options lose their values.
    public async Task UpdateOptionsAsync(Guid id, CustomFieldOptionsRequest request, CancellationToken cancellationToken)
    {
        var field = await db.CustomFields.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Feld nicht gefunden.");
        if (field.Type != CustomFieldType.Select)
            throw new DomainValidationException("Nur Auswahlfelder haben Optionen.");
        var changes = request.Options.Select(x => new { x.Id, Name = x.Name.Trim() }).ToList();
        if (changes.Count == 0)
            throw new DomainValidationException("Für ein Auswahlfeld sind Optionen erforderlich.");
        if (changes.Any(x => x.Name.Length is 0 or > 500))
            throw new DomainValidationException("Optionen müssen 1 bis 500 Zeichen enthalten.");
        if (changes.Select(x => x.Name).Distinct().Count() != changes.Count)
            throw new DomainValidationException("Jede Option darf nur einmal vorkommen.");
        var kept = changes.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToList();
        if (kept.Any(x => field.Options.All(option => option.Id != x)) || kept.Distinct().Count() != kept.Count)
            throw new DomainValidationException("Die Optionen wurden inzwischen geändert. Bitte lade die Seite neu.");

        var removed = field.Options.Select(x => x.Id).Except(kept).Select(x => x.ToString()).ToList();
        // Items in the trash lose the value as well, so a restored item never refers to a removed option.
        if (removed.Count > 0)
            db.ItemFieldValues.RemoveRange(await db.ItemFieldValues.IgnoreQueryFilters()
                .Where(x => x.FieldId == id && removed.Contains(x.Value)).ToListAsync(cancellationToken));
        field.Options = changes.Select(x => new SelectOption(x.Id ?? Guid.NewGuid(), x.Name)).ToList();
        await db.SaveChangesAsync(cancellationToken);
    }

    // Values are removed by the database cascade; views drop every reference to the field.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var field = await db.CustomFields.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Feld nicht gefunden.");
        var key = FieldKeys.ForCustomField(id);
        foreach (var view in await db.ViewConfigurations.Where(x => x.ProjectId == field.ProjectId).ToListAsync(cancellationToken))
        {
            view.VisibleFields = view.VisibleFields.Where(x => x != key).ToList();
            view.Filters = view.Filters.Where(x => x.Field != key).ToList();
            if (view.GroupByField == key) view.GroupByField = null;
            if (view.SortField == key) view.SortField = null;
        }
        db.CustomFields.Remove(field);
        await db.SaveChangesAsync(cancellationToken);
    }
}
