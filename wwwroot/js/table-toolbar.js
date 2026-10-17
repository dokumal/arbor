const groups = [
    ['Zeile', [['＋ oben', 'addRowBefore', 'Zeile darüber einfügen'], ['＋ unten', 'addRowAfter', 'Zeile darunter einfügen'], ['✕', 'deleteRow', 'Zeile löschen']]],
    ['Spalte', [['＋ links', 'addColumnBefore', 'Spalte links einfügen'], ['＋ rechts', 'addColumnAfter', 'Spalte rechts einfügen'], ['✕', 'deleteColumn', 'Spalte löschen']]],
    ['', [['Kopfzeile', 'toggleHeaderRow', 'Kopfzeile ein- oder ausschalten'], ['Tabelle ✕', 'deleteTable', 'Tabelle löschen']]]
];

// Shows row and column commands above the table that holds the cursor (below it when there is no room above).
export function attachTableToolbar(editor, host) {
    const bar = document.createElement('div');
    bar.className = 'table-toolbar';
    bar.hidden = true;
    bar.setAttribute('role', 'toolbar');
    bar.setAttribute('aria-label', 'Tabelle bearbeiten');
    bar.innerHTML = groups.map(([label, buttons]) => `<div class="table-toolbar-group">${label ? `<span>${label}</span>` : ''}${buttons.map(([text, command, title]) =>
        `<button type="button" data-command="${command}" title="${title}" aria-label="${title}"${command.startsWith('delete') ? ' class="danger"' : ''}>${text}</button>`).join('')}</div>`).join('');
    // Keeps the focus and the cell selection in the editor while a button is clicked.
    bar.addEventListener('mousedown', event => event.preventDefault());
    bar.addEventListener('click', event => {
        const button = event.target.closest('[data-command]');
        if (button) editor.chain().focus()[button.dataset.command]().run();
    });
    host.append(bar);

    const currentTable = () => {
        const { $from } = editor.state.selection;
        for (let depth = $from.depth; depth > 0; depth--)
            if ($from.node(depth).type.name === 'table') return editor.view.nodeDOM($from.before(depth));
        return null;
    };
    const update = () => {
        const table = editor.isFocused ? currentTable() : null;
        bar.hidden = !table;
        if (!table) return;
        bar.querySelectorAll('[data-command]').forEach(button => button.disabled = !editor.can()[button.dataset.command]());
        const hostRect = host.getBoundingClientRect();
        const rect = table.getBoundingClientRect();
        const above = rect.top - hostRect.top - bar.offsetHeight - 4;
        bar.style.top = `${above >= 0 ? above : rect.bottom - hostRect.top + 4}px`;
        bar.style.left = `${Math.max(4, Math.min(rect.left - hostRect.left, host.clientWidth - bar.offsetWidth - 4))}px`;
    };
    ['update', 'selectionUpdate', 'focus', 'blur'].forEach(name => editor.on(name, update));
    editor.on('destroy', () => bar.remove());
}
