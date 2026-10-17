using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class StatusService(AppDbContext db, ProjectService projects)
{
    // Categories are stored as text, so the database would sort them alphabetically; order them in memory instead.
    public async Task<List<TaskStatusDefinition>> ListAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await db.Statuses.AsNoTracking().Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken))
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();

    // New tasks start with the first open status of their project.
    public async Task<Guid> DefaultIdAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Statuses.Where(x => x.ProjectId == projectId && x.Category == StatusCategory.Open)
            .OrderBy(x => x.SortOrder).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken)
        ?? throw new DomainValidationException("Es gibt keinen offenen Status.");

    // Saves the complete list. Renamed statuses keep their id; tasks and saved filters of removed statuses
    // move to the replacement, by default the first remaining status of the same category.
    public async Task SaveAsync(Guid projectId, TaskStatusesRequest request, CancellationToken cancellationToken)
    {
        await projects.EnsureExistsAsync(projectId, cancellationToken);
        var changes = request.Statuses.Select(x => new
            { x.Id, Name = x.Name.Trim(), x.Category, Color = x.Color.Trim() }).ToList();
        if (changes.Any(x => x.Name.Length is 0 or > 50))
            throw new DomainValidationException("Statusnamen müssen 1 bis 50 Zeichen enthalten.");
        if (changes.Select(x => x.Name.ToLowerInvariant()).Distinct().Count() != changes.Count)
            throw new DomainValidationException("Jeder Statusname darf nur einmal vorkommen.");
        if (changes.Any(x => !Enum.IsDefined(x.Category)))
            throw new DomainValidationException("Ungültige Statuskategorie.");
        if (Enum.GetValues<StatusCategory>().Any(category => changes.All(x => x.Category != category)))
            throw new DomainValidationException("Jede Kategorie braucht mindestens einen Status.");
        if (changes.Any(x => !StatusColors.All.Contains(x.Color)))
            throw new DomainValidationException("Ungültige Statusfarbe.");

        var existing = await db.Statuses.Where(x => x.ProjectId == projectId).ToDictionaryAsync(x => x.Id, cancellationToken);
        var kept = changes.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToList();
        if (kept.Any(x => !existing.ContainsKey(x)) || kept.Distinct().Count() != kept.Count)
            throw new DomainValidationException("Die Status wurden inzwischen geändert. Bitte lade die Seite neu.");

        var saved = new List<TaskStatusDefinition>();
        foreach (var change in changes)
        {
            TaskStatusDefinition status;
            if (change.Id is Guid id) status = existing[id];
            else
            {
                status = new TaskStatusDefinition { ProjectId = projectId };
                db.Statuses.Add(status);
            }
            status.Name = change.Name;
            status.Category = change.Category;
            status.Color = change.Color;
            status.SortOrder = saved.Count(x => x.Category == change.Category);
            saved.Add(status);
        }

        var removed = existing.Values.Where(x => !kept.Contains(x.Id)).ToList();
        if (removed.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        var views = await db.ViewConfigurations.Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
        foreach (var status in removed)
        {
            var replacement = request.Replacements.TryGetValue(status.Id, out var replacementId)
                ? saved.FirstOrDefault(x => x.Id == replacementId && kept.Contains(x.Id))
                    ?? throw new DomainValidationException($"Der Ersatz für „{status.Name}“ muss ein bestehender Status sein.")
                : saved.First(x => x.Category == status.Category);
            foreach (var task in await db.Items.Where(x => x.StatusId == status.Id).ToListAsync(cancellationToken))
            {
                task.StatusId = replacement.Id;
                task.UpdatedAt = DateTimeOffset.UtcNow;
            }
            var oldValue = status.Id.ToString();
            foreach (var view in views.Where(view => view.Filters.Any(filter => IsFilterOn(filter, oldValue))))
                view.Filters = view.Filters.Select(filter => IsFilterOn(filter, oldValue)
                    ? filter with { Value = replacement.Id.ToString() } : filter).ToList();
            db.Statuses.Remove(status);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsFilterOn(ViewFilter filter, string statusId) =>
        filter.Field == FieldKeys.Status && string.Equals(filter.Value.Trim(), statusId, StringComparison.OrdinalIgnoreCase);
}
