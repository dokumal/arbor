import { api } from './api.js';
import { escapeHtml, fieldLabel, kinds, renderView, statuses, descriptionText } from './render.js?v=inline-fields';
import { getDescription, setDescription } from './description-editor.js';
import { initializeTaskDisplay } from './task-display.js';
import { initializeTreeDrag } from './tree-drag.js?v=folders';
import { initializeTimelineDrag } from './timeline-drag.js';
import { timelineIntervals, shiftTimelineDate } from './timeline-range.js';
import { isDocument, isFolder, isTask, canContain, itemIcon, itemKinds } from './item-relations.js?v=folders';
import { initializeItemLinks } from './item-links.js?v=folders';

const $ = id => document.getElementById(id);
initializeTaskDisplay($('task-drawer'), $('task-display-toggle'));
const now = new Date();
const localMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
const today = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
const savedTimelineInterval = localStorage.getItem('arbor-timeline-interval');
const ui = {
    viewId: localStorage.getItem('arbor-view'), kind: localStorage.getItem('arbor-kind'),
    selectedTaskId: null,
    expanded: new Set(), seenTasks: new Set(), collapsedGroups: new Set(), month: localMonth,
    timelineDate: today.toISOString().slice(0, 10),
    timelineInterval: Object.hasOwn(timelineIntervals, savedTimelineInterval) ? savedTimelineInterval : 'Month',
    editingViewId: null, search: ''
};
let workspace = { items: [], matchingItemIds: [], customFields: [], views: [], links: [] };
let dragTaskId = null;
let dragGroupKey = null;
let toastTimer;

const currentView = () => workspace.views.find(view => view.id === ui.viewId && view.kind === ui.kind) || null;
const taskById = id => workspace.items.find(task => task.id === id);
const itemLinks = initializeItemLinks(() => workspace, () => loadWorkspace(), notify);
initializeTreeDrag($('view-content'), () => workspace.items, async (id, move) => {
    try {
        await api(`/api/items/${id}/move`, { method: 'POST', body: JSON.stringify(move) });
        if (move.parentId) ui.expanded.add(move.parentId);
        await loadWorkspace();
        notify('Eintrag verschoben.');
    } catch (error) { notify(error.message); }
});

initializeTimelineDrag($('view-content'), taskById, async (id, dates) => {
    await api(`/api/items/${id}/dates`, { method: 'PATCH', body: JSON.stringify(dates) });
    await loadWorkspace();
    notify('Zeitraum gespeichert.');
}, error => notify(error.message));

async function loadWorkspace(preferredViewId = ui.viewId, preferredKind = ui.kind) {
    const query = preferredViewId ? `?viewId=${encodeURIComponent(preferredViewId)}` : '';
    workspace = await api(`/api/workspace${query}`);
    const requestedView = workspace.views.find(view => view.id === preferredViewId);
    ui.kind = kinds[preferredKind] ? preferredKind : requestedView?.kind || 'Tree';
    const available = workspace.views.filter(view => view.kind === ui.kind);
    const selected = available.find(view => view.id === preferredViewId)
        || available.find(view => view.id === localStorage.getItem(`arbor-view-${ui.kind}`))
        || available[0] || null;
    ui.viewId = selected?.id || null;
    if (ui.viewId && ui.viewId !== preferredViewId)
        workspace = await api(`/api/workspace?viewId=${encodeURIComponent(ui.viewId)}`);
    localStorage.setItem('arbor-kind', ui.kind);
    if (ui.viewId) {
        localStorage.setItem('arbor-view', ui.viewId);
        localStorage.setItem(`arbor-view-${ui.kind}`, ui.viewId);
    } else localStorage.removeItem('arbor-view');
    workspace.items.forEach(task => {
        if (!ui.seenTasks.has(task.id)) {
            ui.expanded.add(task.id);
            ui.seenTasks.add(task.id);
        }
    });
    render();
}

function render() {
    const view = currentView();
    const documents = workspace.items.filter(isDocument).length;
    const tasks = workspace.items.filter(isTask).length;
    const folders = workspace.items.filter(isFolder).length;
    $('task-count').textContent = `${tasks} ${tasks === 1 ? 'Aufgabe' : 'Aufgaben'} · ${documents} ${documents === 1 ? 'Dokument' : 'Dokumente'} · ${folders} Ordner`;
    $('view-tabs').innerHTML = Object.entries(kinds).map(([kind, details]) =>
        `<button class="view-tab ${kind === ui.kind ? 'active' : ''}" data-action="select-kind" data-kind="${kind}" role="tab" aria-selected="${kind === ui.kind}"><span class="tab-symbol">${details.icon}</span>${escapeHtml(details.label)}</button>`).join('');
    $('config-tabs').innerHTML = workspace.views.filter(item => item.kind === ui.kind).map(item =>
        `<span class="config-item ${item.id === ui.viewId ? 'active' : ''}"><button class="config-tab" data-action="select-view" data-id="${item.id}" role="tab" aria-selected="${item.id === ui.viewId}">${escapeHtml(item.name)}</button><button class="config-edit" data-action="edit-view" data-id="${item.id}" title="${escapeHtml(item.name)} bearbeiten" aria-label="${escapeHtml(item.name)} bearbeiten">⋯</button></span>`).join('');
    $('view-icon').textContent = kinds[ui.kind]?.icon || '☷';
    $('view-title').textContent = view?.name || 'Noch keine Konfiguration';
    $('view-description').textContent = kinds[ui.kind]?.description || '';
    $('filter-summary').textContent = view?.filters.length ? `${view.filters.length} Filter aktiv` : '';
    $('edit-view-button').hidden = !view;
    const query = ui.search.trim().toLocaleLowerCase('de');
    const terms = query.split(/\s+/).filter(Boolean);
    const matchesSearch = item => {
        const text = [item.title, descriptionText(item.description), ...Object.values(item.fields)].join(' ').toLocaleLowerCase('de');
        return terms.every(term => text.includes(term));
    };
    const data = { ...workspace, searchQuery: query, matchingItemIds: workspace.matchingItemIds.filter(id => matchesSearch(taskById(id))) };
    // Re-rendering replaces the table, so keep the cell being edited focused (e.g. after tabbing on from a saved cell).
    const editing = $('view-content').contains(document.activeElement) && document.activeElement.matches('[data-inline-field]') ? document.activeElement : null;
    $('view-content').innerHTML = view ? renderView(data, view, ui)
        : `<div class="empty-state"><h3>Keine Konfiguration für ${escapeHtml(kinds[ui.kind]?.label || 'diese Sicht')}</h3><p>Erstelle eine Konfiguration mit deinen gewünschten Filtern und Feldern.</p><button class="primary-button" data-action="new-view">＋ Konfiguration erstellen</button></div>`;
    if (editing) {
        const replacement = $('view-content').querySelector(`[data-item-id="${editing.dataset.itemId}"][data-inline-field="${editing.dataset.inlineField}"]`);
        if (replacement) { replacement.value = editing.value; replacement.focus(); }
    }
}

async function saveInlineField(input) {
    const task = taskById(input.dataset.itemId);
    const fieldId = input.dataset.inlineField;
    const value = input.value.trim() || null;
    if (!task || (task.fields[fieldId] ?? null) === value) return;
    try {
        await api(`/api/items/${task.id}/fields/${fieldId}`, { method: 'PATCH', body: JSON.stringify({ value }) });
        await loadWorkspace();
    } catch (error) {
        notify(error.message);
        render();
    }
}

function notify(message) {
    clearTimeout(toastTimer);
    $('toast').textContent = message;
    $('toast').hidden = false;
    toastTimer = setTimeout(() => $('toast').hidden = true, 3000);
}

function showError(id, error) { $(id).textContent = error?.message || String(error); }

function descendantsOf(id) {
    const result = new Set([id]);
    let changed = true;
    while (changed) {
        changed = false;
        workspace.items.forEach(task => {
            if (result.has(task.parentId) && !result.has(task.id)) {
                result.add(task.id); changed = true;
            }
        });
    }
    return result;
}

function customInput(field, value) {
    let input;
    if (field.type === 'Select') input = `<select data-field-id="${field.id}"><option value="">—</option>${field.options.map(option => `<option value="${escapeHtml(option)}" ${value === option ? 'selected' : ''}>${escapeHtml(option)}</option>`).join('')}</select>`;
    else if (field.type === 'Boolean') input = `<select data-field-id="${field.id}"><option value="">—</option><option value="true" ${value === 'true' ? 'selected' : ''}>Ja</option><option value="false" ${value === 'false' ? 'selected' : ''}>Nein</option></select>`;
    else input = `<input data-field-id="${field.id}" type="${{ Number: 'number', Date: 'date' }[field.type] || 'text'}" ${field.type === 'Number' ? 'step="any"' : ''} value="${escapeHtml(value || '')}" maxlength="500" />`;
    return `<label class="custom-field"><span>${escapeHtml(field.name)}</span>${input}</label>`;
}

function drawerFieldValues() {
    return Object.fromEntries(Array.from($('task-custom-fields').querySelectorAll('[data-field-id]'))
        .map(input => [input.dataset.fieldId, input.value]));
}

function refreshDrawerFields(values) {
    if ($('task-drawer').hidden) return;
    const task = taskById($('task-id').value);
    if (isFolder(task) || $('item-kind').value === 'Folder') return;
    $('task-custom-fields').innerHTML = workspace.customFields.map(field =>
        customInput(field, values[field.id] ?? task?.fields[field.id])).join('') ||
        '<p class="helper-text">Noch keine eigenen Felder.</p>';
}

function openTask(id = null, options = {}) {
    const task = id ? taskById(id) : null;
    const kind = task?.kind || options.kind || 'Task';
    const document = kind === 'Document';
    const folder = kind === 'Folder';
    $('task-form').reset();
    $('task-form-error').textContent = '';
    $('task-id').value = task?.id || '';
    $('item-kind').value = kind;
    const captions = { Task: ['NEUE AUFGABE', 'AUFGABE BEARBEITEN'], Document: ['NEUES DOKUMENT', 'DOKUMENT BEARBEITEN'], Folder: ['NEUER ORDNER', 'ORDNER BEARBEITEN'] };
    $('drawer-caption').textContent = captions[kind][task ? 1 : 0];
    $('item-content-label').textContent = document ? 'Inhalt' : 'Beschreibung';
    $('task-title').placeholder = folder ? 'Titel des Ordners' : document ? 'Titel des Dokuments' : 'Was steht an?';
    ['item-status-field', 'item-start-field', 'item-due-field'].forEach(field => $(field).hidden = kind !== 'Task');
    $('item-custom-fields-section').hidden = folder;
    $('task-drawer').classList.toggle('document-editor', document);
    $('task-drawer').classList.toggle('folder-editor', folder);
    $('task-title').value = task?.title || '';
    $('task-status').value = task?.status || options.status || 'Todo';
    $('task-start').value = task?.startDate || '';
    $('task-due').value = task?.dueDate || '';
    const excluded = task ? descendantsOf(task.id) : new Set();
    $('task-parent').innerHTML = '<option value="">Keine</option>' + workspace.items.filter(item => !excluded.has(item.id) && canContain(item.kind, kind)).map(item => `<option value="${item.id}">${itemIcon(item)} ${escapeHtml(item.title)}</option>`).join('');
    $('task-parent').value = task?.parentId || options.parentId || '';
    $('task-custom-fields').innerHTML = folder ? '' : workspace.customFields.map(field => customInput(field, task?.fields[field.id])).join('') || '<p class="helper-text">Noch keine eigenen Felder.</p>';
    $('delete-task-button').hidden = !task;
    const children = task ? workspace.items.filter(item => item.parentId === task.id) : [];
    $('task-children-section').hidden = !task;
    $('add-child-folder-button').hidden = !folder;
    $('task-children').innerHTML = children.map(child => `<button type="button" class="child-link" data-action="open-task" data-id="${child.id}">${itemIcon(child)} ${escapeHtml(child.title)}${isTask(child) ? `<span class="related-status">${escapeHtml(statuses[child.status])}</span>` : ''}</button>`).join('') || '<p class="helper-text">Noch keine untergeordneten Einträge.</p>';
    itemLinks.render(task?.id || null);
    ui.selectedTaskId = task?.id || null;
    $('drawer-backdrop').hidden = false;
    $('task-drawer').hidden = false;
    $('task-drawer').setAttribute('aria-hidden', 'false');
    setDescription(task?.description || '');
    render();
    $('task-title').focus();
}

function closeTask() {
    $('task-drawer').hidden = true;
    $('drawer-backdrop').hidden = true;
    $('task-drawer').setAttribute('aria-hidden', 'true');
    ui.selectedTaskId = null;
    render();
}

async function saveTask(event) {
    event.preventDefault();
    $('task-form-error').textContent = '';
    const id = $('task-id').value;
    const kind = $('item-kind').value;
    const fields = {};
    $('task-custom-fields').querySelectorAll('[data-field-id]').forEach(input => fields[input.dataset.fieldId] = input.value || null);
    const payload = {
        kind,
        title: $('task-title').value.trim(), description: getDescription(),
        parentId: $('task-parent').value || null, status: kind === 'Task' ? $('task-status').value : 'Todo',
        startDate: kind === 'Task' ? $('task-start').value || null : null,
        dueDate: kind === 'Task' ? $('task-due').value || null : null, fields
    };
    try {
        await api(id ? `/api/items/${id}` : '/api/items', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload) });
        closeTask();
        await loadWorkspace(kind !== 'Task' && ui.kind !== 'Tree' ? null : ui.viewId, kind !== 'Task' ? 'Tree' : ui.kind);
        notify(`${itemKinds[kind]} ${id ? 'gespeichert' : 'erstellt'}.`);
    } catch (error) { showError('task-form-error', error); }
}

async function deleteTask() {
    const id = $('task-id').value;
    if (!id || !confirm('Diesen Eintrag samt allen untergeordneten Ordnern, Aufgaben und Dokumenten wirklich löschen?')) return;
    try { await api(`/api/items/${id}`, { method: 'DELETE' }); closeTask(); await loadWorkspace(); notify('Eintrag gelöscht.'); }
    catch (error) { showError('task-form-error', error); }
}

const builtinFields = [
    ['title', 'Eintrag'], ['kind', 'Typ'], ['status', 'Status'], ['startDate', 'Startdatum'],
    ['dueDate', 'Fälligkeitsdatum'], ['description', 'Inhalt']
];
function allFieldChoices() { return [...builtinFields, ...workspace.customFields.map(field => [`field:${field.id}`, field.name])]; }

function updateGroupingSettings() {
    const calendar = $('config-kind').value === 'Calendar';
    $('grouping-settings').hidden = calendar;
    $('config-group-by').disabled = calendar;
    if (calendar) $('config-group-by').value = '';
}

function filterValueControl(fieldKey, value = '') {
    const field = workspace.customFields.find(item => fieldKey === `field:${item.id}`);
    const choices = fieldKey === 'kind' ? Object.entries(itemKinds) : fieldKey === 'status' ? Object.entries(statuses) :
        field?.type === 'Select' ? field.options.map(option => [option, option]) :
        field?.type === 'Boolean' ? [['true', 'Ja'], ['false', 'Nein']] : null;
    if (choices) return `<select class="filter-value" aria-label="Filterwert"><option value="">Wert wählen</option>${choices.map(([key, label]) => `<option value="${escapeHtml(key)}" ${value === key ? 'selected' : ''}>${escapeHtml(label)}</option>`).join('')}</select>`;
    const type = ['startDate', 'dueDate'].includes(fieldKey) || field?.type === 'Date' ? 'date' : field?.type === 'Number' ? 'number' : 'text';
    return `<input class="filter-value" aria-label="Filterwert" type="${type}" ${type === 'number' ? 'step="any"' : ''} maxlength="500" value="${escapeHtml(value)}" placeholder="Wert" />`;
}

function openViewDialog(id = null) {
    const view = id ? workspace.views.find(item => item.id === id) : null;
    ui.editingViewId = view?.id || null;
    $('view-form').reset(); $('view-form-error').textContent = '';
    $('view-dialog-heading').textContent = view ? 'Konfiguration bearbeiten' : 'Neue Konfiguration';
    $('config-name').value = view?.name || '';
    $('config-kind').value = view?.kind || ui.kind || 'Tree';
    $('config-group-by').innerHTML = '<option value="">Keine Gruppierung</option>' +
        allFieldChoices().map(([key, label]) => `<option value="${escapeHtml(key)}">${escapeHtml(label)}</option>`).join('');
    $('config-group-by').value = view?.groupByField || '';
    updateGroupingSettings();
    $('delete-view-button').hidden = !view;
    const selected = new Set(view?.visibleFields || ['title', 'status', 'dueDate']);
    $('visible-fields').innerHTML = allFieldChoices().map(([key, label]) => `<label><input type="checkbox" value="${key}" ${selected.has(key) ? 'checked' : ''} ${key === 'title' ? 'disabled' : ''} />${escapeHtml(label)}</label>`).join('');
    $('filter-rows').innerHTML = '';
    (view?.filters || []).forEach(addFilterRow);
    $('view-dialog').showModal();
    $('config-name').focus();
}

function addFilterRow(filter = { field: 'status', operator: 'equals', value: '' }) {
    const row = document.createElement('div'); row.className = 'filter-row';
    const fields = allFieldChoices().map(([key, label]) => `<option value="${key}" ${filter.field === key ? 'selected' : ''}>${escapeHtml(label)}</option>`).join('');
    const operators = [['equals', 'ist'], ['notEquals', 'ist nicht'], ['contains', 'enthält'], ['isEmpty', 'ist leer'], ['isNotEmpty', 'ist nicht leer'], ['before', 'vor'], ['after', 'nach'], ['greaterThan', 'größer als'], ['lessThan', 'kleiner als']];
    row.innerHTML = `<select class="filter-field" aria-label="Filterfeld">${fields}</select><select class="filter-operator" aria-label="Operator">${operators.map(([value, label]) => `<option value="${value}" ${filter.operator === value ? 'selected' : ''}>${label}</option>`).join('')}</select>${filterValueControl(filter.field, filter.value)}<button type="button" class="filter-remove" data-action="remove-filter" aria-label="Filter entfernen">×</button>`;
    row.querySelector('.filter-value').disabled = ['isEmpty', 'isNotEmpty'].includes(filter.operator);
    $('filter-rows').append(row);
}

async function saveView(event) {
    event.preventDefault(); $('view-form-error').textContent = '';
    const visibleFields = ['title', ...Array.from($('visible-fields').querySelectorAll('input:checked')).map(input => input.value)].filter((value, index, values) => values.indexOf(value) === index);
    const filters = Array.from($('filter-rows').querySelectorAll('.filter-row')).map(row => ({ field: row.querySelector('.filter-field').value, operator: row.querySelector('.filter-operator').value, value: row.querySelector('.filter-value').value }));
    const payload = { name: $('config-name').value.trim(), kind: $('config-kind').value,
        groupByField: $('config-kind').value === 'Calendar' ? null : $('config-group-by').value || null,
        visibleFields, filters };
    try {
        const id = ui.editingViewId;
        const result = await api(id ? `/api/views/${id}` : '/api/views', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload) });
        $('view-dialog').close(); await loadWorkspace(id || result.id, payload.kind); notify('Konfiguration gespeichert.');
    } catch (error) { showError('view-form-error', error); }
}

async function deleteView() {
    const id = ui.editingViewId;
    if (!id || !confirm('Diese Konfiguration wirklich löschen?')) return;
    try {
        await api(`/api/views/${id}`, { method: 'DELETE' });
        $('view-dialog').close();
        if (id === ui.viewId) {
            localStorage.removeItem(`arbor-view-${ui.kind}`);
            ui.viewId = null;
        }
        await loadWorkspace(ui.viewId, ui.kind);
        notify('Konfiguration gelöscht.');
    }
    catch (error) { showError('view-form-error', error); }
}

function openFieldDialog() {
    $('field-form-error').textContent = '';
    $('field-list').innerHTML = workspace.customFields.map(field => `<div class="field-list-item"><strong>${escapeHtml(field.name)}</strong><span>${{ Text: 'Text', Select: 'Auswahl', Number: 'Zahl', Date: 'Datum', Boolean: 'Ja / Nein' }[field.type]}</span><button type="button" class="icon-button" data-action="delete-field" data-id="${field.id}" title="Feld löschen" aria-label="${escapeHtml(field.name)} löschen">×</button></div>`).join('') || '<p class="helper-text">Noch keine eigenen Felder.</p>';
    $('field-dialog').showModal();
}

async function saveField(event) {
    event.preventDefault(); $('field-form-error').textContent = '';
    const previousValues = drawerFieldValues();
    const payload = { name: $('field-name').value.trim(), type: $('field-type').value,
        options: $('field-options').value.split('\n').map(item => item.trim()).filter(Boolean) };
    try {
        await api('/api/fields', { method: 'POST', body: JSON.stringify(payload) });
        $('field-dialog').close(); $('field-form').reset(); await loadWorkspace();
        refreshDrawerFields(previousValues);
        notify('Feld erstellt.');
    } catch (error) { showError('field-form-error', error); }
}

async function deleteField(id) {
    const field = workspace.customFields.find(item => item.id === id);
    if (!confirm(`Feld „${field?.name || ''}“ und alle zugehörigen Werte löschen?`)) return;
    const previousValues = drawerFieldValues();
    try { await api(`/api/fields/${id}`, { method: 'DELETE' }); $('field-dialog').close(); await loadWorkspace(); refreshDrawerFields(previousValues); notify('Feld gelöscht.'); }
    catch (error) { showError('field-form-error', error); }
}

function shiftMonth(amount) {
    const [year, month] = ui.month.split('-').map(Number);
    ui.month = new Date(Date.UTC(year, month - 1 + amount, 1)).toISOString().slice(0, 7);
    render();
}
function shiftTimeline(amount) {
    ui.timelineDate = shiftTimelineDate(ui.timelineDate, ui.timelineInterval, amount);
    render();
}

document.addEventListener('click', async event => {
    if (event.target.closest('.cell-edit')) return;
    const action = event.target.closest('[data-action]');
    if (!action) return;
    switch (action.dataset.action) {
        case 'select-kind': await loadWorkspace(null, action.dataset.kind); break;
        case 'select-view': await loadWorkspace(action.dataset.id, ui.kind); break;
        case 'edit-view': openViewDialog(action.dataset.id); break;
        case 'new-view': openViewDialog(); break;
        case 'new-task': openTask(null, { status: action.dataset.status, parentId: action.dataset.parentId }); break;
        case 'new-document': openTask(null, { kind: 'Document', parentId: action.dataset.parentId }); break;
        case 'new-folder': openTask(null, { kind: 'Folder', parentId: action.dataset.parentId }); break;
        case 'open-task': openTask(action.dataset.id); break;
        case 'toggle': event.stopPropagation(); ui.expanded.has(action.dataset.id) ? ui.expanded.delete(action.dataset.id) : ui.expanded.add(action.dataset.id); render(); break;
        case 'toggle-group': {
            const key = JSON.stringify([ui.viewId, action.dataset.group]);
            ui.collapsedGroups.has(key) ? ui.collapsedGroups.delete(key) : ui.collapsedGroups.add(key);
            render(); break;
        }
        case 'calendar-prev': shiftMonth(-1); break;
        case 'calendar-next': shiftMonth(1); break;
        case 'timeline-prev': shiftTimeline(-1); break;
        case 'timeline-next': shiftTimeline(1); break;
        case 'timeline-interval':
            if (!Object.hasOwn(timelineIntervals, action.dataset.interval)) break;
            ui.timelineInterval = action.dataset.interval;
            localStorage.setItem('arbor-timeline-interval', ui.timelineInterval);
            render(); break;
        case 'remove-filter': action.closest('.filter-row').remove(); break;
        case 'delete-field': await deleteField(action.dataset.id); break;
    }
});

document.addEventListener('dragstart', event => {
    const card = event.target.closest('.kanban-card');
    if (!card) return;
    dragTaskId = card.dataset.id; dragGroupKey = card.dataset.group ?? null;
    event.dataTransfer.effectAllowed = 'move';
    card.classList.add('dragging');
});
document.addEventListener('dragend', event => {
    event.target.closest('.kanban-card')?.classList.remove('dragging');
    document.querySelectorAll('.kanban-column').forEach(column => column.classList.remove('drag-over'));
    dragTaskId = null; dragGroupKey = null;
});
document.addEventListener('dragover', event => {
    const column = event.target.closest('.kanban-column');
    if (!column || !dragTaskId) return;
    if (dragGroupKey !== null && column.dataset.group !== dragGroupKey && currentView()?.groupByField !== 'status') return;
    event.preventDefault(); column.classList.add('drag-over');
});
document.addEventListener('dragleave', event => event.target.closest('.kanban-column')?.classList.remove('drag-over'));
document.addEventListener('drop', async event => {
    const column = event.target.closest('.kanban-column');
    if (!column || !dragTaskId) return;
    event.preventDefault(); column.classList.remove('drag-over');
    const task = taskById(dragTaskId);
    if (dragGroupKey !== null && column.dataset.group !== dragGroupKey && currentView()?.groupByField !== 'status') {
        notify('Aufgaben können nur innerhalb ihrer Gruppe verschoben werden.'); return;
    }
    dragTaskId = null; dragGroupKey = null;
    if (!task || task.status === column.dataset.status) return;
    try {
        await api(`/api/items/${task.id}`, { method: 'PUT', body: JSON.stringify({ ...task, status: column.dataset.status }) });
        await loadWorkspace(); notify('Status geändert.');
    } catch (error) { notify(error.message); }
});

$('new-task-button').addEventListener('click', () => openTask());
$('new-folder-button').addEventListener('click', () => openTask(null, { kind: 'Folder' }));
$('new-document-button').addEventListener('click', () => openTask(null, { kind: 'Document' }));
$('workspace-search').addEventListener('input', event => { ui.search = event.target.value; render(); });
$('config-kind').addEventListener('change', updateGroupingSettings);
$('close-task-button').addEventListener('click', closeTask);
$('drawer-backdrop').addEventListener('click', closeTask);
$('task-form').addEventListener('submit', saveTask);
$('delete-task-button').addEventListener('click', deleteTask);
$('add-child-button').addEventListener('click', () => openTask(null, { parentId: $('task-id').value }));
$('add-child-document-button').addEventListener('click', () => openTask(null, { kind: 'Document', parentId: $('task-id').value }));
$('add-child-folder-button').addEventListener('click', () => openTask(null, { kind: 'Folder', parentId: $('task-id').value }));
$('field-manager-button').addEventListener('click', openFieldDialog);
$('drawer-add-field').addEventListener('click', openFieldDialog);
$('add-view-button').addEventListener('click', () => openViewDialog());
$('edit-view-button').addEventListener('click', () => openViewDialog(currentView()?.id));
$('add-filter-button').addEventListener('click', () => addFilterRow());
$('view-form').addEventListener('submit', saveView);
$('delete-view-button').addEventListener('click', deleteView);
$('field-form').addEventListener('submit', saveField);
$('field-type').addEventListener('change', () => $('field-options-wrap').hidden = $('field-type').value !== 'Select');
document.addEventListener('change', event => {
    if (event.target.matches('[data-inline-field]')) saveInlineField(event.target);
    if (event.target.matches('.filter-field')) {
        const row = event.target.closest('.filter-row');
        row.querySelector('.filter-value').outerHTML = filterValueControl(event.target.value);
    }
    if (event.target.matches('.filter-field, .filter-operator')) {
        const row = event.target.closest('.filter-row');
        const empty = ['isEmpty', 'isNotEmpty'].includes(row.querySelector('.filter-operator').value);
        row.querySelector('.filter-value').disabled = empty;
    }
});
document.querySelectorAll('.dialog-close').forEach(button => button.addEventListener('click', () => $(button.dataset.dialog).close()));
document.addEventListener('keydown', event => {
    if (!event.target.matches('input[data-inline-field]')) return;
    if (event.key === 'Enter') event.target.blur();
    if (event.key === 'Escape') {
        event.target.value = taskById(event.target.dataset.itemId)?.fields[event.target.dataset.inlineField] || '';
        event.target.blur();
    }
});
document.addEventListener('keydown', event => { if (event.key === 'Escape' && !$('task-drawer').hidden && !document.querySelector('dialog[open]')) closeTask(); });

loadWorkspace().catch(error => { $('view-content').innerHTML = `<div class="empty-state"><h3>Workspace konnte nicht geladen werden</h3><p>${escapeHtml(error.message)}</p></div>`; });
