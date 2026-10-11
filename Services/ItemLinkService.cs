using Microsoft.EntityFrameworkCore;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class ItemLinkService(AppDbContext db)
{
    public async Task AddAsync(Guid itemId, Guid relatedId, CancellationToken cancellationToken)
    {
        var item = await db.Items.FindAsync([itemId], cancellationToken)
            ?? throw new EntityNotFoundException("Eintrag nicht gefunden.");
        var related = await db.Items.FindAsync([relatedId], cancellationToken)
            ?? throw new EntityNotFoundException("Verknüpfter Eintrag nicht gefunden.");
        if (!ItemKindRules.CanLink(item.Kind, related.Kind))
            throw new DomainValidationException("Eine Verknüpfung verbindet eine Aufgabe mit einem Dokument.");
        var (taskId, documentId) = item.Kind == WorkspaceItemKind.Task ? (item.Id, related.Id) : (related.Id, item.Id);
        if (await db.ItemLinks.AnyAsync(x => x.TaskId == taskId && x.DocumentId == documentId, cancellationToken)) return;
        db.ItemLinks.Add(new ItemLink { TaskId = taskId, DocumentId = documentId });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid itemId, Guid relatedId, CancellationToken cancellationToken)
    {
        var link = await db.ItemLinks.FirstOrDefaultAsync(x =>
            (x.TaskId == itemId && x.DocumentId == relatedId) ||
            (x.DocumentId == itemId && x.TaskId == relatedId), cancellationToken);
        if (link is null) return;
        db.ItemLinks.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }
}
