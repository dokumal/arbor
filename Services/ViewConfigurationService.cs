using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class ViewConfigurationService(AppDbContext db)
{
    private static readonly HashSet<string> Operators = ["equals", "notEquals", "contains", "isEmpty",
        "isNotEmpty", "before", "after", "greaterThan", "lessThan"];

    public async Task<Guid> CreateAsync(ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request, cancellationToken);
        var view = new ViewConfiguration
        {
            SortOrder = (await db.ViewConfigurations.MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1
        };
        Apply(view, request);
        db.ViewConfigurations.Add(view);
        await db.SaveChangesAsync(cancellationToken);
        return view.Id;
    }

    public async Task UpdateAsync(Guid id, ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        var view = await FindAsync(id, cancellationToken);
        await ValidateAsync(request, cancellationToken);
        Apply(view, request);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var view = await FindAsync(id, cancellationToken);
        if (await db.ViewConfigurations.CountAsync(cancellationToken) == 1)
            throw new DomainValidationException("Die letzte Ansicht kann nicht gelöscht werden.");
        db.ViewConfigurations.Remove(view);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ViewConfiguration> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ViewConfigurations.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Ansicht nicht gefunden.");

    private static void Apply(ViewConfiguration view, ViewConfigurationSaveRequest request)
    {
        view.Name = request.Name.Trim();
        view.Kind = request.Kind;
        view.Filters = request.Filters.ToList();
        view.VisibleFields = request.VisibleFields.Distinct().ToList();
        view.GroupByField = request.GroupByField;
        view.SortField = request.SortField;
        view.SortDescending = request.SortDescending;
        view.HideCompleted = request.HideCompleted;
    }

    private async Task ValidateAsync(ViewConfigurationSaveRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new DomainValidationException("Bitte einen Namen eingeben.");
        if (!Enum.IsDefined(request.Kind)) throw new DomainValidationException("Ungültige Darstellung.");
        if (request.Filters.Count > 20) throw new DomainValidationException("Maximal 20 Filter pro Ansicht.");
        var fields = (await db.CustomFields.Select(x => x.Id).ToListAsync(cancellationToken))
            .Select(FieldKeys.ForCustomField).ToHashSet();
        fields.UnionWith(FieldKeys.BuiltIn);
        if (request.VisibleFields.Any(x => !fields.Contains(x)) ||
            request.Filters.Any(x => !fields.Contains(x.Field)))
            throw new DomainValidationException("Ein ausgewähltes Feld existiert nicht.");
        // The calendar is laid out by date, so it can't be grouped or sorted.
        if (request.GroupByField is not null &&
            (!fields.Contains(request.GroupByField) || request.Kind == ViewKind.Calendar))
            throw new DomainValidationException("Das Gruppierungsfeld ist für diese Darstellung ungültig.");
        if (request.SortField is not null &&
            (!fields.Contains(request.SortField) || request.SortField == FieldKeys.Description || request.Kind == ViewKind.Calendar))
            throw new DomainValidationException("Das Sortierfeld ist für diese Darstellung ungültig.");
        if (request.Filters.Any(x => !Operators.Contains(x.Operator) || x.Value is null || x.Value.Length > 500))
            throw new DomainValidationException("Ein Filter ist ungültig.");
    }
}
