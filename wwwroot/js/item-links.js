import { api } from './api.js?v=problem-details';
import { escapeHtml } from './render.js?v=statuses';
import { statusName } from './statuses.js';
import { relatedItems, isDocument, isFolder, itemIcon } from './item-relations.js?v=folders';

export function initializeItemLinks(getWorkspace, refreshWorkspace, notify) {
    const section = document.getElementById('item-links-section');
    const list = document.getElementById('item-links');
    const picker = document.getElementById('related-item-picker');
    const error = document.getElementById('item-links-error');
    let itemId = null;

    function render(id) {
        itemId = id;
        const workspace = getWorkspace();
        const current = workspace.items.find(item => item.id === id);
        section.hidden = !current || isFolder(current);
        error.textContent = '';
        if (!current || isFolder(current)) return;
        document.getElementById('item-links-heading').textContent = isDocument(current) ? 'Zugehörige Aufgaben' : 'Zugehörige Dokumente';
        const related = relatedItems(workspace.items, workspace.links, id);
        list.innerHTML = related.map(({ item, explicit, inTree }) => `<div class="related-item"><button type="button" class="related-item-open" data-action="open-task" data-id="${item.id}"><span>${itemIcon(item)}</span><strong>${escapeHtml(item.title)}</strong>${isDocument(item) ? '' : `<span class="related-status">${escapeHtml(statusName(item.statusId))}</span>`}</button><span class="relation-source">${inTree ? 'Im Baum' : 'Verknüpft'}</span>${explicit ? `<button type="button" class="icon-button unlink-item" data-id="${item.id}" aria-label="Verknüpfung entfernen" title="Verknüpfung entfernen">×</button>` : ''}</div>`).join('') || '<p class="helper-text">Noch keine zugehörigen Einträge.</p>';
        const existing = new Set(related.map(relation => relation.item.id));
        const choices = workspace.items.filter(item => item.kind === (isDocument(current) ? 'Task' : 'Document') && !existing.has(item.id));
        picker.innerHTML = `<option value="">${isDocument(current) ? 'Aufgabe auswählen …' : 'Dokument auswählen …'}</option>` + choices.map(item => `<option value="${item.id}">${escapeHtml(item.title)}</option>`).join('');
        document.getElementById('add-item-link').disabled = !choices.length;
    }

    async function change(path, options) {
        error.textContent = '';
        try { await api(path, options); await refreshWorkspace(); render(itemId); notify('Verknüpfung gespeichert.'); }
        catch (exception) { error.textContent = exception.message; }
    }
    document.getElementById('add-item-link').addEventListener('click', async () => {
        if (!itemId || !picker.value) { error.textContent = 'Bitte einen Eintrag auswählen.'; return; }
        await change(`/api/items/${itemId}/links`, { method: 'POST', body: JSON.stringify({ relatedItemId: picker.value }) });
    });
    list.addEventListener('click', async event => {
        const button = event.target.closest('.unlink-item');
        if (!button || !itemId) return;
        await change(`/api/items/${itemId}/links/${button.dataset.id}`, { method: 'DELETE' });
    });
    return { render };
}
