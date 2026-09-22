import { api } from './api.js';
import { escapeHtml, fieldLabel, kinds, renderView, statuses } from './render.js';

const $ = id => document.getElementById(id);
const now = new Date();
const localMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
const monday = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
monday.setUTCDate(monday.getUTCDate() - (monday.getUTCDay() + 6) % 7);
const ui = {
    viewId: localStorage.getItem('arbor-view'), selectedTaskId: null,
    expanded: new Set(), seenTasks: new Set(), month: localMonth,
    timelineWeek: monday.toISOString().slice(0, 10), editingViewId: null
};
let workspace = { tasks: [], matchingTaskIds: [], customFields: [], views: [] };
let dragTaskId = null;
let toastTimer;

const currentView = () => workspace.views.find(view => view.id === ui.viewId) || workspace.views[0];
const taskById = id => workspace.tasks.find(task => task.id === id);

async function loadWorkspace(preferredViewId = ui.viewId) {
    const query = preferredViewId ? `?viewId=${encodeURIComponent(preferredViewId)}` : '';
    workspace = await api(`/api/workspace${query}`);
    if (!workspace.views.some(view => view.id === preferredViewId)) {
        ui.viewId = workspace.views[0]?.id || null;
        if (ui.viewId && ui.viewId !== preferredViewId) {
            workspace = await api(`/api/workspace?viewId=${encodeURIComponent(ui.viewId)}`);
        }
    } else ui.viewId = preferredViewId;
    if (ui.viewId) localStorage.setItem('arbor-view', ui.viewId);
    workspace.tasks.forEach(task => {
        if (!ui.seenTasks.has(task.id)) {
            ui.expanded.add(task.id);
            ui.seenTasks.add(task.id);
        }
    });
    render();
}

function render() {
    const view = currentView();
    $('task-count').textContent = `${workspace.tasks.length} ${workspace.tasks.length === 1 ? 'Aufgabe' : 'Aufgaben'}`;
    $('view-tabs').innerHTML = workspace.views.map(item => `<button class="view-tab ${item.id === ui.viewId ? 'active' : ''}" data-action="select-view" data-id="${item.id}" role="tab" aria-selected="${item.id === ui.viewId}"><span class="tab-symbol">${kinds[item.kind]?.icon || '☷'}</span>${escapeHtml(item.name)}</button>`).join('');
    $('view-icon').textContent = kinds[view?.kind]?.icon || '☷';
    $('view-title').textContent = view?.name || 'Aufgaben';
    $('view-description').textContent = kinds[view?.kind]?.description || '';
    $('filter-summary').textContent = view?.filters.length ? `${view.filters.length} ${view.filters.length === 1 ? 'Filter aktiv' : 'Filter aktiv'}` : '';
    $('view-content').innerHTML = renderView(workspace, view, ui);
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
        workspace.tasks.forEach(task => {
            if (result.has(task.parentTaskId) && !result.has(task.id)) {
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
    $('task-custom-fields').innerHTML = workspace.customFields.map(field =>
        customInput(field, values[field.id] ?? task?.fields[field.id])).join('') ||
        '<p class="helper-text">Noch keine eigenen Felder.</p>';
}

function openTask(id = null, options = {}) {
    const task = id ? taskById(id) : null;
    $('task-form').reset();
    $('task-form-error').textContent = '';
    $('task-id').value = task?.id || '';
    $('drawer-caption').textContent = task ? 'AUFGABE BEARBEITEN' : 'NEUE AUFGABE';
    $('task-title').value = task?.title || '';
    $('task-description').value = task?.description || '';
    $('task-status').value = task?.status || options.status || 'Todo';
    $('task-start').value = task?.startDate || '';
    $('task-due').value = task?.dueDate || '';
    const excluded = task ? descendantsOf(task.id) : new Set();
    $('task-parent').innerHTML = '<option value="">Keine</option>' + workspace.tasks.filter(item => !excluded.has(item.id)).map(item => `<option value="${item.id}">${escapeHtml(item.title)}</option>`).join('');
    $('task-parent').value = task?.parentTaskId || options.parentTaskId || '';
    $('task-custom-fields').innerHTML = workspace.customFields.map(field => customInput(field, task?.fields[field.id])).join('') || '<p class="helper-text">Noch keine eigenen Felder.</p>';
    $('delete-task-button').hidden = !task;
    const children = task ? workspace.tasks.filter(item => item.parentTaskId === task.id) : [];
    $('task-children-section').hidden = !task;
    $('task-children').innerHTML = children.map(child => `<button type="button" class="child-link" data-action="open-task" data-id="${child.id}">↳ ${escapeHtml(child.title)}</button>`).join('') || '<p class="helper-text">Noch keine Unteraufgaben.</p>';
    ui.selectedTaskId = task?.id || null;
    $('drawer-backdrop').hidden = false;
    $('task-drawer').hidden = false;
    $('task-drawer').setAttribute('aria-hidden', 'false');
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
    const fields = {};
    $('task-custom-fields').querySelectorAll('[data-field-id]').forEach(input => fields[input.dataset.fieldId] = input.value || null);
    const payload = {
        title: $('task-title').value.trim(), description: $('task-description').value,
        parentTaskId: $('task-parent').value || null, status: $('task-status').value,
        startDate: $('task-start').value || null, dueDate: $('task-due').value || null, fields
    };
    try {
        await api(id ? `/api/tasks/${id}` : '/api/tasks', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload) });
        closeTask(); await loadWorkspace(); notify(id ? 'Aufgabe gespeichert.' : 'Aufgabe erstellt.');
    } catch (error) { showError('task-form-error', error); }
}

async function deleteTask() {
    const id = $('task-id').value;
    if (!id || !confirm('Diese Aufgabe und ihre Unteraufgaben wirklich löschen?')) return;
    try { await api(`/api/tasks/${id}`, { method: 'DELETE' }); closeTask(); await loadWorkspace(); notify('Aufgabe gelöscht.'); }
    catch (error) { showError('task-form-error', error); }
}

const builtinFields = [
    ['title', 'Aufgabe'], ['status', 'Status'], ['startDate', 'Startdatum'],
    ['dueDate', 'Fälligkeitsdatum'], ['description', 'Beschreibung']
];
function allFieldChoices() { return [...builtinFields, ...workspace.customFields.map(field => [`field:${field.id}`, field.name])]; }

function filterValueControl(fieldKey, value = '') {
    const field = workspace.customFields.find(item => fieldKey === `field:${item.id}`);
    const choices = fieldKey === 'status' ? Object.entries(statuses) :
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
    $('view-dialog-heading').textContent = view ? 'Ansicht konfigurieren' : 'Neue Ansicht';
    $('config-name').value = view?.name || '';
    $('config-kind').value = view?.kind || 'Tree';
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
    const payload = { name: $('config-name').value.trim(), kind: $('config-kind').value, visibleFields, filters };
    try {
        const id = ui.editingViewId;
        const result = await api(id ? `/api/views/${id}` : '/api/views', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload) });
        $('view-dialog').close(); await loadWorkspace(id || result.id); notify('Ansicht gespeichert.');
    } catch (error) { showError('view-form-error', error); }
}

async function deleteView() {
    const id = ui.editingViewId;
    if (!id || !confirm('Diese Ansicht wirklich löschen?')) return;
    try { await api(`/api/views/${id}`, { method: 'DELETE' }); $('view-dialog').close(); ui.viewId = null; await loadWorkspace(null); notify('Ansicht gelöscht.'); }
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
function shiftWeek(amount) {
    const date = new Date(`${ui.timelineWeek}T00:00:00Z`);
    date.setUTCDate(date.getUTCDate() + amount * 28);
    ui.timelineWeek = date.toISOString().slice(0, 10); render();
}

document.addEventListener('click', async event => {
    const action = event.target.closest('[data-action]');
    if (!action) return;
    switch (action.dataset.action) {
        case 'select-view': await loadWorkspace(action.dataset.id); break;
        case 'new-task': openTask(null, { status: action.dataset.status }); break;
        case 'open-task': openTask(action.dataset.id); break;
        case 'toggle': event.stopPropagation(); ui.expanded.has(action.dataset.id) ? ui.expanded.delete(action.dataset.id) : ui.expanded.add(action.dataset.id); render(); break;
        case 'calendar-prev': shiftMonth(-1); break;
        case 'calendar-next': shiftMonth(1); break;
        case 'timeline-prev': shiftWeek(-1); break;
        case 'timeline-next': shiftWeek(1); break;
        case 'remove-filter': action.closest('.filter-row').remove(); break;
        case 'delete-field': await deleteField(action.dataset.id); break;
    }
});

document.addEventListener('dragstart', event => {
    const card = event.target.closest('.kanban-card');
    if (!card) return;
    dragTaskId = card.dataset.id; event.dataTransfer.effectAllowed = 'move';
    card.classList.add('dragging');
});
document.addEventListener('dragend', event => {
    event.target.closest('.kanban-card')?.classList.remove('dragging');
    document.querySelectorAll('.kanban-column').forEach(column => column.classList.remove('drag-over'));
});
document.addEventListener('dragover', event => {
    const column = event.target.closest('.kanban-column');
    if (!column || !dragTaskId) return;
    event.preventDefault(); column.classList.add('drag-over');
});
document.addEventListener('dragleave', event => event.target.closest('.kanban-column')?.classList.remove('drag-over'));
document.addEventListener('drop', async event => {
    const column = event.target.closest('.kanban-column');
    if (!column || !dragTaskId) return;
    event.preventDefault(); column.classList.remove('drag-over');
    const task = taskById(dragTaskId); dragTaskId = null;
    if (!task || task.status === column.dataset.status) return;
    try {
        await api(`/api/tasks/${task.id}`, { method: 'PUT', body: JSON.stringify({ ...task, status: column.dataset.status }) });
        await loadWorkspace(); notify('Status geändert.');
    } catch (error) { notify(error.message); }
});

$('new-task-button').addEventListener('click', () => openTask());
$('close-task-button').addEventListener('click', closeTask);
$('drawer-backdrop').addEventListener('click', closeTask);
$('task-form').addEventListener('submit', saveTask);
$('delete-task-button').addEventListener('click', deleteTask);
$('add-child-button').addEventListener('click', () => openTask(null, { parentTaskId: $('task-id').value }));
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
document.addEventListener('keydown', event => { if (event.key === 'Escape' && !$('task-drawer').hidden && !document.querySelector('dialog[open]')) closeTask(); });

loadWorkspace().catch(error => { $('view-content').innerHTML = `<div class="empty-state"><h3>Workspace konnte nicht geladen werden</h3><p>${escapeHtml(error.message)}</p></div>`; });
