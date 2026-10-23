using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

/// <summary>
/// Deleting an item moves it with everything below it into the trash, from where it can be restored or deleted for good.
/// Only the deleted item itself is a trash entry; the items below it carry its id in <see cref="WorkspaceItem.DeletedWithId"/>.
/// No live item ever sits below an item in the trash.
/// </summary>
public sealed class TrashService(AppDbContext db)
{
    private IQueryable<WorkspaceItem> AllItems => db.Items.IgnoreQueryFilters();

    public async Task MoveToTrashAsync(Guid id, CancellationToken cancellationToken)
    {
        var projectId = await db.Items.Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Eintrag nicht gefunden.");
        // Only live items: descendants that are already in the trash stay separate entries.
        var tree = await db.Items.Where(x => x.ProjectId == projectId)
            .Select(x => new TreeNode(x.Id, x.ParentId)).ToListAsync(cancellationToken);
        var ids = Subtree(id, tree);
        var now = DateTimeOffset.UtcNow;
        await db.Items.Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.DeletedAt, now)
            .SetProperty(x => x.DeletedWithId, id), cancellationToken);
    }

    public async Task<List<TrashEntryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var projectIds = await AllItems.Where(x => x.DeletedWithId != null).Select(x => x.ProjectId).Distinct()
            .ToListAsync(cancellationToken);
        if (projectIds.Count == 0) return [];
        var projects = await db.Projects.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var items = await AllItems.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId))
            .Select(x => new { x.Id, x.ParentId, x.ProjectId, x.Kind, x.Title, x.DeletedAt, x.DeletedWithId })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var contained = items.Values.Where(x => x.DeletedWithId is not null)
            .GroupBy(x => x.DeletedWithId!.Value).ToDictionary(x => x.Key, x => x.Count() - 1);

        // The location lists the titles from the top level down to the former parent.
        List<string> Location(Guid? parentId)
        {
            var titles = new List<string>();
            while (parentId is Guid current && items.TryGetValue(current, out var parent))
            {
                titles.Insert(0, parent.Title);
                parentId = parent.ParentId;
            }
            return titles;
        }

        // SQLite can't sort by DateTimeOffset, so the entries are ordered here.
        return items.Values.Where(x => x.DeletedWithId == x.Id)
            .OrderByDescending(x => x.DeletedAt).ThenBy(x => x.Title)
            .Select(x => new TrashEntryDto(x.Id, x.ProjectId, projects.GetValueOrDefault(x.ProjectId, ""), x.Kind, x.Title,
                x.DeletedAt!.Value, contained.GetValueOrDefault(x.Id), Location(x.ParentId),
                x.ParentId is Guid parentId && items[parentId].DeletedAt is not null))
            .ToList();
    }

    // Restores the entry with everything that went into the trash with it. If its former parent is in the trash
    // itself, it goes to the nearest ancestor that isn't. The kind rules allow that: all ancestors of a folder are folders.
    public async Task<TrashRestoreResultDto> RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await AllItems.FirstOrDefaultAsync(x => x.Id == id && x.DeletedWithId == id, cancellationToken)
            ?? throw new EntityNotFoundException("Der Eintrag liegt nicht mehr im Papierkorb.");
        var parentId = item.ParentId;
        string? parentTitle = null;
        while (parentId is Guid current)
        {
            var parent = await AllItems.Where(x => x.Id == current)
                .Select(x => new { x.ParentId, x.Title, x.DeletedAt }).FirstAsync(cancellationToken);
            if (parent.DeletedAt is null)
            {
                parentTitle = parent.Title;
                break;
            }
            parentId = parent.ParentId;
        }
        var moved = parentId != item.ParentId;

        // Back to its former position among the siblings, or at the end of the level it moved to.
        var siblings = await db.Items.Where(x => x.ProjectId == item.ProjectId && x.ParentId == parentId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        siblings.Insert(moved ? siblings.Count : Math.Clamp(item.SortOrder, 0, siblings.Count), item);
        for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i;
        item.ParentId = parentId;
        item.DeletedAt = null;
        item.DeletedWithId = null;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await AllItems.Where(x => x.DeletedWithId == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.DeletedAt, (DateTimeOffset?)null)
            .SetProperty(x => x.DeletedWithId, (Guid?)null), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var message = !moved ? $"„{item.Title}“ wurde wiederhergestellt."
            : parentTitle is null ? $"„{item.Title}“ wurde auf der obersten Ebene wiederhergestellt, weil der ursprüngliche Ort im Papierkorb liegt."
            : $"„{item.Title}“ wurde unter „{parentTitle}“ wiederhergestellt, weil der ursprüngliche Ort im Papierkorb liegt.";
        return new TrashRestoreResultDto(item.ProjectId, message);
    }

    // Deletes the entry and everything below it for good. Entries below it that went into the trash on their own
    // remain there; they move up to the entry's parent.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await AllItems.Where(x => x.Id == id && x.DeletedWithId == id)
            .Select(x => new { x.ProjectId, x.ParentId }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new EntityNotFoundException("Der Eintrag liegt nicht mehr im Papierkorb.");
        var tree = await AllItems.Where(x => x.ProjectId == item.ProjectId)
            .Select(x => new { x.Id, x.ParentId, x.DeletedWithId }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var below = Subtree(id, tree.Values.Select(x => new TreeNode(x.Id, x.ParentId))).Skip(1).ToHashSet();
        bool IsEntry(Guid itemId) => tree[itemId].DeletedWithId == itemId;
        // Only the topmost ones move; entries inside them keep their place.
        bool HasEntryAbove(Guid itemId)
        {
            for (var parentId = tree[itemId].ParentId; parentId is Guid current && current != id; parentId = tree[current].ParentId)
                if (IsEntry(current)) return true;
            return false;
        }
        var separate = below.Where(x => IsEntry(x) && !HasEntryAbove(x)).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (separate.Count > 0)
            await AllItems.Where(x => separate.Contains(x.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ParentId, item.ParentId), cancellationToken);
        // The database cascade removes the descendants with their field values.
        await AllItems.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Nothing live sits below an item in the trash, so this removes exactly the trash.
    public async Task EmptyAsync(CancellationToken cancellationToken) =>
        await AllItems.Where(x => x.DeletedAt != null).ExecuteDeleteAsync(cancellationToken);

    private sealed record TreeNode(Guid Id, Guid? ParentId);

    // The given item followed by all of its descendants.
    private static List<Guid> Subtree(Guid rootId, IEnumerable<TreeNode> items)
    {
        var children = items.Where(x => x.ParentId is not null).ToLookup(x => x.ParentId!.Value, x => x.Id);
        var result = new List<Guid> { rootId };
        for (var i = 0; i < result.Count; i++) result.AddRange(children[result[i]]);
        return result;
    }
}
