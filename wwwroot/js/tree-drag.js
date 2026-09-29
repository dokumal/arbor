// Compute a move from the actual hierarchy, including siblings hidden by filters.
export function resolveTreeMove(tasks, draggedId, targetId, placement, levelsUp = 0) {
    const byId = new Map(tasks.map(task => [task.id, task]));
    const dragged = byId.get(draggedId);
    let target = byId.get(targetId);
    if (!dragged || !target) return null;
    while (levelsUp-- > 0 && target?.parentTaskId) target = byId.get(target.parentTaskId);
    if (!target || target.id === draggedId) return null;
    const parentTaskId = placement === 'inside' ? target.id : target.parentTaskId;
    let ancestor = parentTaskId;
    const visited = new Set([draggedId]);
    while (ancestor) {
        if (visited.has(ancestor)) return null;
        visited.add(ancestor);
        ancestor = byId.get(ancestor)?.parentTaskId;
    }
    let beforeTaskId = null;
    if (placement === 'before') beforeTaskId = target.id;
    if (placement === 'after') {
        const siblings = tasks.filter(task => task.parentTaskId === parentTaskId && task.id !== draggedId)
            .sort((a, b) => a.sortOrder - b.sortOrder || a.id.localeCompare(b.id));
        beforeTaskId = siblings[siblings.findIndex(task => task.id === target.id) + 1]?.id || null;
    }
    return { parentTaskId, beforeTaskId, targetId: target.id, placement };
}

export function initializeTreeDrag(content, getTasks, onMove) {
    let draggedId = null;
    let destination = null;
    let busy = false;
    const hint = document.createElement('div');
    hint.className = 'tree-drop-hint';
    hint.hidden = true;
    document.body.append(hint);

    function clearIndicator() {
        content.querySelectorAll('.tree-drop-before,.tree-drop-after,.tree-drop-inside').forEach(row => {
            row.classList.remove('tree-drop-before', 'tree-drop-after', 'tree-drop-inside');
            row.style.removeProperty('--drop-indent');
        });
        destination = null;
        hint.hidden = true;
    }
    function finish() {
        clearIndicator();
        content.querySelector('.tree-dragging')?.classList.remove('tree-dragging');
        draggedId = null;
    }
    content.addEventListener('click', event => {
        if (event.target.closest('[data-tree-drag]')) event.stopPropagation();
    });
    content.addEventListener('dragstart', event => {
        const handle = event.target.closest('[data-tree-drag]');
        if (!handle) return;
        if (busy) { event.preventDefault(); return; }
        draggedId = handle.dataset.treeDrag;
        handle.closest('.task-row').classList.add('tree-dragging');
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', draggedId);
        event.dataTransfer.setDragImage(handle.closest('.task-row'), 30, 20);
    });
    content.addEventListener('dragover', event => {
        if (!draggedId) return;
        clearIndicator();
        const row = event.target.closest('.task-row');
        if (!row) { event.dataTransfer.dropEffect = 'none'; return; }
        const rect = row.getBoundingClientRect();
        const relativeY = (event.clientY - rect.top) / rect.height;
        const depth = Number(row.dataset.depth);
        const displayDepth = Math.min(depth, 12);
        const titleLeft = row.querySelector('.tree-title').getBoundingClientRect().left;
        const desiredDepth = Math.max(0, Math.min(displayDepth, Math.floor((event.clientX - titleLeft) / 22)));
        const placement = relativeY < .25 ? 'before' : relativeY > .75 || desiredDepth < displayDepth ? 'after' : 'inside';
        const move = resolveTreeMove(getTasks(), draggedId, row.dataset.id, placement,
            placement === 'inside' ? 0 : displayDepth - desiredDepth);
        if (!move) { event.dataTransfer.dropEffect = 'none'; return; }
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        destination = move;
        let marker = [...content.querySelectorAll('.task-row')].find(item => item.dataset.id === move.targetId);
        if (!marker) { clearIndicator(); return; }
        const markerDepth = Number(marker.dataset.depth);
        if (placement === 'after') {
            // Place the line after the whole visible subtree, not above its children.
            let next = marker.nextElementSibling;
            while (next?.classList.contains('task-row') && Number(next.dataset.depth) > markerDepth) {
                marker = next; next = next.nextElementSibling;
            }
        }
        marker.style.setProperty('--drop-indent', `${27 + Math.min(markerDepth, 12) * 22}px`);
        marker.classList.add(`tree-drop-${placement}`);
        const target = getTasks().find(task => task.id === move.targetId);
        hint.textContent = placement === 'inside' ? `Unteraufgabe von „${target.title}“` :
            `${placement === 'before' ? 'Vor' : 'Nach'} „${target.title}“ · gleiche Ebene`;
        hint.style.left = `${Math.max(8, Math.min(event.clientX + 16, window.innerWidth - 300))}px`;
        hint.style.top = `${Math.max(8, Math.min(event.clientY + 18, window.innerHeight - 50))}px`;
        hint.hidden = false;
        if (event.clientY < 65) window.scrollBy(0, -18);
        else if (event.clientY > window.innerHeight - 65) window.scrollBy(0, 18);
    });
    content.addEventListener('dragleave', event => {
        if (!content.contains(event.relatedTarget)) clearIndicator();
    });
    content.addEventListener('drop', async event => {
        if (!draggedId || !destination) return;
        event.preventDefault();
        const id = draggedId;
        const { parentTaskId, beforeTaskId } = destination;
        finish(); busy = true;
        try { await onMove(id, { parentTaskId, beforeTaskId }); }
        finally { busy = false; }
    });
    document.addEventListener('dragend', finish);
}
