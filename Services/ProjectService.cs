using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class ProjectService(AppDbContext db)
{
    public const int MaxNameLength = 100;

    public async Task<List<ProjectDto>> ListAsync(CancellationToken cancellationToken) =>
        await db.Projects.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ProjectDto(x.Id, x.Name, x.SortOrder)).ToListAsync(cancellationToken);

    public async Task<ProjectDto?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Projects.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ProjectDto(x.Id, x.Name, x.SortOrder)).FirstOrDefaultAsync(cancellationToken);

    // The project to open on the start page: the remembered one while it exists, otherwise the first.
    public async Task<Guid> StartProjectIdAsync(Guid? remembered, CancellationToken cancellationToken)
    {
        if (remembered is Guid id && await db.Projects.AnyAsync(x => x.Id == id, cancellationToken)) return id;
        return await db.Projects.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Id).FirstAsync(cancellationToken);
    }

    // Throws when a project id from the address or a request does not exist (any more).
    public async Task EnsureExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(x => x.Id == id, cancellationToken))
            throw new EntityNotFoundException("Projekt nicht gefunden.");
    }

    // A new project starts with the default statuses and views, but without custom fields.
    public async Task<ProjectDto> CreateAsync(ProjectSaveRequest request, CancellationToken cancellationToken)
    {
        var project = new Project
        {
            Name = await ValidNameAsync(request.Name, null, cancellationToken),
            SortOrder = (await db.Projects.MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? -1) + 1
        };
        db.Projects.Add(project);
        db.Statuses.AddRange(ProjectDefaults.Statuses(project.Id));
        db.ViewConfigurations.AddRange(ProjectDefaults.Views(project.Id));
        await db.SaveChangesAsync(cancellationToken);
        return new ProjectDto(project.Id, project.Name, project.SortOrder);
    }

    public async Task RenameAsync(Guid id, ProjectSaveRequest request, CancellationToken cancellationToken)
    {
        var project = await db.Projects.FindAsync([id], cancellationToken)
            ?? throw new EntityNotFoundException("Projekt nicht gefunden.");
        project.Name = await ValidNameAsync(request.Name, id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    // Deletes the project with all of its items, statuses, fields and views.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureExistsAsync(id, cancellationToken);
        if (await db.Projects.CountAsync(cancellationToken) == 1)
            throw new DomainValidationException("Das letzte Projekt kann nicht gelöscht werden.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Items first, including those in the trash: their children and field values go with them,
        // and afterwards no item refers to a status.
        await db.Items.IgnoreQueryFilters().Where(x => x.ProjectId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Statuses.Where(x => x.ProjectId == id).ExecuteDeleteAsync(cancellationToken);
        await db.CustomFields.Where(x => x.ProjectId == id).ExecuteDeleteAsync(cancellationToken);
        await db.ViewConfigurations.Where(x => x.ProjectId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Projects.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<string> ValidNameAsync(string name, Guid? id, CancellationToken cancellationToken)
    {
        name = name.Trim();
        if (name.Length is 0 or > MaxNameLength)
            throw new DomainValidationException($"Der Projektname muss 1 bis {MaxNameLength} Zeichen enthalten.");
        if (await db.Projects.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new DomainValidationException("Ein Projekt mit diesem Namen existiert bereits.");
        return name;
    }
}
