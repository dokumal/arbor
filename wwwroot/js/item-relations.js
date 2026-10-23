export const itemKinds = { Folder: 'Ordner', Task: 'Aufgabe', Document: 'Dokument' };
export const isDocument = item => item?.kind === 'Document';
export const isFolder = item => item?.kind === 'Folder';
export const isTask = item => item?.kind === 'Task';
export const itemIcon = item => isFolder(item) ? '📁' : isDocument(item) ? '📄' : '☐';
export const canContain = (parentKind, childKind) => childKind !== 'Folder' || parentKind === 'Folder';

// Related items come from the tree alone: the nearest ancestor of the other kind, and the descendants
// of the other kind reached through items of the same kind. Folders end the search.
export function relatedItems(items, id) {
    const byId = new Map(items.map(item => [item.id, item]));
    const current = byId.get(id);
    if (!current || isFolder(current)) return [];
    const relatedKind = isTask(current) ? 'Document' : 'Task';
    const related = [];
    const add = item => { if (item.kind === relatedKind) related.push(item); };
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
    return related.sort((a, b) => a.title.localeCompare(b.title, 'de'));
}
