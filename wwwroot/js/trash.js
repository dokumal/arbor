import { api } from './api.js?v=problem-details';
import { escapeHtml } from './render.js?v=statuses';
import { itemIcon, itemKinds } from './item-relations.js?v=folders';

const $ = id => document.getElementById(id);
let entries = [];

const dayKey = date => `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
function formatDeleted(value) {
    const date = new Date(value);
    const time = date.toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' });
    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);
    if (dayKey(date) === dayKey(new Date())) return `heute, ${time}`;
    if (dayKey(date) === dayKey(yesterday)) return `gestern, ${time}`;
    return `${date.toLocaleDateString('de-DE', { day: 'numeric', month: 'short', year: 'numeric' })}, ${time}`;
}

function entryHtml(entry) {
    const location = entry.location.length ? `in ${entry.location.join(' › ')}${entry.parentInTrash ? ' (im Papierkorb)' : ''}` : 'oberste Ebene';
    const contained = entry.containedCount ? `mit ${entry.containedCount} ${entry.containedCount === 1 ? 'untergeordnetem Eintrag' : 'untergeordneten Einträgen'}` : '';
    const meta = [itemKinds[entry.kind], location, `gelöscht ${formatDeleted(entry.deletedAt)}`, contained].filter(Boolean);
    return `<li class="trash-entry"><span class="trash-icon" aria-hidden="true">${itemIcon(entry)}</span><div class="trash-main"><strong>${escapeHtml(entry.title)}</strong><span class="trash-meta">${meta.map(escapeHtml).join(' · ')}</span></div><div class="trash-actions"><button type="button" class="secondary-button" data-action="restore" data-id="${entry.id}">Wiederherstellen</button><button type="button" class="danger-button" data-action="delete" data-id="${entry.id}" aria-label="„${escapeHtml(entry.title)}“ endgültig löschen">Endgültig löschen</button></div></li>`;
}

function render() {
    $('empty-trash-button').hidden = !entries.length;
    if (!entries.length) {
        $('trash-content').innerHTML = '<div class="empty-state trash-empty"><h3>Der Papierkorb ist leer</h3><p>Gelöschte Ordner, Aufgaben und Dokumente erscheinen hier.</p></div>';
        return;
    }
    // Grouped by project, in the order of the most recent deletion.
    const groups = new Map();
    for (const entry of entries) {
        if (!groups.has(entry.projectId)) groups.set(entry.projectId, { name: entry.projectName, entries: [] });
        groups.get(entry.projectId).entries.push(entry);
    }
    $('trash-content').innerHTML = [...groups.values()].map(group =>
        `<section class="trash-group"><h2>${escapeHtml(group.name)}<span class="group-count">${group.entries.length}</span></h2><ul class="trash-list">${group.entries.map(entryHtml).join('')}</ul></section>`).join('');
}

function setStatus(message, { error = false, projectId = null } = {}) {
    const status = $('trash-status');
    status.textContent = message;
    if (projectId) {
        const link = document.createElement('a');
        link.href = `/projekte/${projectId}`;
        link.textContent = 'Zum Projekt';
        status.append(' ', link);
    }
    status.classList.toggle('error', error);
    status.hidden = !message;
}

async function load() {
    try {
        entries = await api('/api/trash');
        render();
    } catch (error) {
        $('trash-content').innerHTML = `<div class="empty-state"><h3>Der Papierkorb konnte nicht geladen werden</h3><p>${escapeHtml(error.message)}</p></div>`;
    }
}

async function run(button, action) {
    button.disabled = true;
    try { await action(); }
    catch (error) { setStatus(error.message, { error: true }); }
    finally { button.disabled = false; }
    await load();
}

$('trash-content').addEventListener('click', event => {
    const button = event.target.closest('[data-action]');
    const entry = button && entries.find(item => item.id === button.dataset.id);
    if (!entry) return;
    if (button.dataset.action === 'restore') run(button, async () => {
        const result = await api(`/api/trash/${entry.id}/restore`, { method: 'POST' });
        setStatus(result.message, { projectId: result.projectId });
    });
    if (button.dataset.action === 'delete') {
        const contained = entry.containedCount ? ` samt ${entry.containedCount} ${entry.containedCount === 1 ? 'untergeordnetem Eintrag' : 'untergeordneten Einträgen'}` : '';
        if (!confirm(`„${entry.title}“${contained} endgültig löschen? Das lässt sich nicht rückgängig machen.`)) return;
        run(button, async () => {
            await api(`/api/trash/${entry.id}`, { method: 'DELETE' });
            setStatus(`„${entry.title}“ wurde endgültig gelöscht.`);
        });
    }
});

$('empty-trash-button').addEventListener('click', event => {
    const count = entries.reduce((sum, entry) => sum + 1 + entry.containedCount, 0);
    const what = count === 1 ? 'Den Eintrag im Papierkorb' : `Alle ${count} Einträge im Papierkorb`;
    if (!confirm(`${what} endgültig löschen? Das lässt sich nicht rückgängig machen.`)) return;
    run(event.currentTarget, async () => {
        await api('/api/trash', { method: 'DELETE' });
        setStatus('Der Papierkorb wurde geleert.');
    });
});

load();
