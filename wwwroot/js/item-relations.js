export const itemKinds = { Folder: 'Ordner', Task: 'Aufgabe', Document: 'Dokument' };
export const isDocument = item => item?.kind === 'Document';
export const isFolder = item => item?.kind === 'Folder';
export const isTask = item => item?.kind === 'Task';
export const itemIcon = item => isFolder(item) ? '📁' : isDocument(item) ? '📄' : '☐';
export const canContain = (parentKind, childKind) => childKind !== 'Folder' || parentKind === 'Folder';

// Tree placement and explicit references are independent; show each related item once.
export function relatedItems(items, links, id) {
    const byId = new Map(items.map(item => [item.id, item]));
    const current = byId.get(id);
    if (!current || isFolder(current)) return [];
    const relatedKind = isTask(current) ? 'Document' : 'Task';
    const related = new Map();
    const add = (item, explicit = false) => {
        if (!item || item.kind !== relatedKind) return;
        const previous = related.get(item.id);
        related.set(item.id, { item, explicit: explicit || previous?.explicit || false,
            inTree: !explicit || previous?.inTree || false });
    };
    const visited = new Set([id]);
    let ancestor = byId.get(current.parentId);
    while (ancestor && !visited.has(ancestor.id)) {
        visited.add(ancestor.id);
        if (isFolder(ancestor)) break;
        if (ancestor.kind !== current.kind) { add(ancestor); break; }
        ancestor = byId.get(ancestor.parentId);
    }
    const walk = parent => {
        for (const child of items.filter(item => item.parentId === parent)) {
            if (visited.has(child.id)) continue;
            visited.add(child.id);
            if (isFolder(child)) continue;
            if (child.kind !== current.kind) add(child);
            else walk(child.id);
        }
    };
    walk(id);
    for (const link of links) {
        if (link.taskId === id) add(byId.get(link.documentId), true);
        if (link.documentId === id) add(byId.get(link.taskId), true);
    }
    return [...related.values()].sort((a, b) => a.item.title.localeCompare(b.item.title, 'de'));
}
