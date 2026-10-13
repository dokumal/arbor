import { getTimelineRange, timelineIntervals } from './timeline-range.js';
import { isDocument, isFolder, isTask, itemIcon, itemKinds, relatedItems } from './item-relations.js?v=folders';
import { prefilledItem, prefilledKeys, viewPrefill } from './view-prefill.js';

export const kinds = {
    Tree: { label: 'Baum', icon: '☷', description: 'Ordner, Aufgaben und Dokumente gemeinsam organisiert' },
    Kanban: { label: 'Kanban', icon: '▥', description: 'Aufgaben nach Status organisiert' },
    Calendar: { label: 'Kalender', icon: '▦', description: 'Termine auf einen Blick' },
    Timeline: { label: 'Zeitachse', icon: '▬', description: 'Aufgaben im zeitlichen Verlauf' }
};
export const statuses = { Todo: 'Offen', Doing: 'In Arbeit', Done: 'Erledigt' };
export const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, character =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]);

export function fieldLabel(key, definitions) {
    if (key.startsWith('field:')) return definitions.find(field => field.id === key.slice(6))?.name || 'Eigenes Feld';
    return { title: 'Eintrag', kind: 'Typ', description: 'Inhalt', status: 'Status', startDate: 'Start', dueDate: 'Fällig' }[key] || key;
}

// Select fields store an option id; unknown values are shown as they are.
export const optionName = (field, value) => field.options.find(option => option.id === value)?.name ?? value;

// Custom field values as the user reads them, for the workspace search.
export const customFieldText = (item, definitions) => Object.entries(item.fields).map(([id, value]) => {
    const field = definitions.find(definition => definition.id === id);
    return field?.type === 'Select' ? optionName(field, value) : value;
});

export function fieldValue(task, key, definitions) {
    if (key.startsWith('field:')) {
        const field = definitions.find(item => item.id === key.slice(6));
        const value = task.fields[key.slice(6)];
        if (field?.type === 'Boolean') return value === 'true' ? 'Ja' : value === 'false' ? 'Nein' : '';
        if (field?.type === 'Date') return formatDate(value);
        if (field?.type === 'Select' && value) return optionName(field, value);
        return value || '';
    }
    if (key === 'kind') return itemKinds[task.kind] || 'Aufgabe';
    if (key === 'status') return isTask(task) ? statuses[task.status] || task.status : '';
    if (key === 'startDate' || key === 'dueDate') return formatDate(task[key]);
    if (key === 'description') return descriptionText(task.description);
    return task[key] || '';
}

export function descriptionText(value) {
    if (!value) return '';
    if (!/<[a-z][^>]*>/i.test(value)) return value;
    const plain = value.replace(/<\/?(?:p|div|li|h[1-6]|tr|br)\b[^>]*>/gi, ' ')
        .replace(/<[^>]*>/g, ' ');
    const decoder = document.createElement('textarea');
    decoder.innerHTML = plain;
    return decoder.value.replace(/\s+/g, ' ').trim();
}

export function formatDate(value, options = { day: '2-digit', month: 'short', year: 'numeric' }) {
    if (!value) return '';
    return new Intl.DateTimeFormat('de-DE', { timeZone: 'UTC', ...options }).format(new Date(`${value}T00:00:00Z`));
}

const localToday = () => {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
};

// 'overdue' or 'today' for open tasks, used to highlight due dates in every view.
export function dueState(task, today = localToday()) {
    if (!isTask(task) || task.status === 'Done' || !task.dueDate) return null;
    return task.dueDate < today ? 'overdue' : task.dueDate === today ? 'today' : null;
}
const dueTitle = { overdue: 'Überfällig', today: 'Heute fällig' };

function sortValue(item, key, definitions) {
    if (key === 'status') return isTask(item) ? Object.keys(statuses).indexOf(item.status) : null;
    if (key === 'kind') return Object.keys(itemKinds).indexOf(item.kind);
    if (!key.startsWith('field:')) return item[key] || null;
    const field = definitions.find(definition => definition.id === key.slice(6));
    const raw = item.fields[key.slice(6)];
    if (!raw) return null;
    if (field?.type === 'Number') return Number.parseFloat(raw);
    if (field?.type === 'Boolean') return raw === 'true' ? 0 : 1;
    if (field?.type === 'Select') {
        const index = field.options.findIndex(option => option.id === raw);
        return index < 0 ? field.options.length : index;
    }
    return raw;
}

// Sorts by the view's sort field; empty values always come last and ties keep the manual order.
export function sortItems(items, view, definitions) {
    if (!view.sortField) return items;
    const values = new Map(items.map(item => [item.id, sortValue(item, view.sortField, definitions)]));
    const direction = view.sortDescending ? -1 : 1;
    return [...items].sort((a, b) => {
        const left = values.get(a.id), right = values.get(b.id);
        if (left === null || right === null) return (left === null) - (right === null);
        const result = typeof left === 'number' && typeof right === 'number' ? left - right
            : String(left).localeCompare(String(right), 'de', { numeric: true });
        return result * direction;
    });
}

const empty = (filtered = false) => `<div class="empty-state"><h3>${filtered ? 'Keine passenden Einträge' : 'Noch keine Einträge'}</h3><p>${filtered ? 'Passe die Suche oder die Filter dieser Ansicht an.' : 'Erstelle einen Ordner, eine Aufgabe oder ein Dokument.'}</p>${filtered ? '' : '<div class="empty-actions"><button class="secondary-button" data-action="new-folder">＋ Ordner</button><button class="primary-button" data-action="new-task">＋ Aufgabe</button><button class="secondary-button" data-action="new-document">＋ Dokument</button></div>'}</div>`;

const createTaskIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 6h9M3 12h7M3 18h9M18 9v8M14 13h8"/></svg>';
const createDocumentIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M12 12v6M9 15h6"/></svg>';
const createFolderIcon = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 7V5a2 2 0 0 1 2-2h5l2 4h7a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7zM12 11v6M9 14h6"/></svg>';

function rowCreateActions(item) {
    return `<div class="row-create-actions">${isFolder(item) ? `<button type="button" class="row-create-button" data-action="new-folder" data-parent-id="${item.id}" title="Unterordner erstellen" aria-label="Ordner unter „${escapeHtml(item.title)}“ erstellen">${createFolderIcon}</button>` : ''}<button type="button" class="row-create-button" data-action="new-task" data-parent-id="${item.id}" title="Unteraufgabe erstellen" aria-label="Aufgabe unter „${escapeHtml(item.title)}“ erstellen">${createTaskIcon}</button><button type="button" class="row-create-button" data-action="new-document" data-parent-id="${item.id}" title="Untergeordnetes Dokument erstellen" aria-label="Dokument unter „${escapeHtml(item.title)}“ erstellen">${createDocumentIcon}</button></div>`;
}

// Creates entries that already meet the view's filters, so they show up in it right away.
function quickAdd(view, definitions) {
    const prefill = viewPrefill(view, definitions);
    if (!prefill.kinds.length) return '';
    const item = prefilledItem(prefill, prefill.kinds[0]);
    const values = prefilledKeys(prefill, prefill.kinds[0]).map(key =>
        `<span class="quick-add-value">${escapeHtml(key === 'title' ? 'Titel' : fieldLabel(key, definitions))}: <strong>${escapeHtml(fieldValue(item, key, definitions))}</strong></span>`).join('');
    return `<div class="quick-add">${prefill.kinds.map(kind => `<button type="button" class="quick-add-button" data-action="quick-add" data-kind="${kind}">＋ ${itemKinds[kind]}</button>`).join('')}${values ? `<span class="quick-add-summary">Vorbelegt aus den Filtern: ${values}</span>` : ''}</div>`;
}

function itemSymbol(item) {
    return isTask(item) ? `<span class="status-dot ${item.status}"></span>` :
        `<span class="${isFolder(item) ? 'folder-symbol' : 'document-symbol'}" title="${itemKinds[item.kind]}">${itemIcon(item)}</span>`;
}

function documentRefs(task) {
    const docs = task.relatedDocuments || [];
    if (!docs.length) return '';
    return `<div class="document-refs">${docs.slice(0, 2).map(doc => `<button type="button" class="document-ref" data-action="open-task" data-id="${doc.id}" title="${escapeHtml(doc.title)}">📄 ${escapeHtml(doc.title)}</button>`).join('')}${docs.length > 2 ? `<button type="button" class="document-ref" data-action="open-task" data-id="${task.id}">+${docs.length - 2}</button>` : ''}</div>`;
}

function groupTasks(tasks, view, definitions) {
    if (!view.groupByField) return null;
    const field = view.groupByField;
    const definition = field.startsWith('field:') ? definitions.find(item => item.id === field.slice(6)) : null;
    const groups = new Map();
    for (const task of tasks) {
        const raw = field === 'status' && !isTask(task) ? null : field.startsWith('field:') ? task.fields[field.slice(6)] :
            field === 'description' ? descriptionText(task.description) : task[field];
        const key = String(raw ?? '').trim();
        if (!groups.has(key)) groups.set(key, { key, label: key ? fieldValue(task, field, definitions) : 'Ohne Wert', tasks: [] });
        groups.get(key).tasks.push(task);
    }
    const order = field === 'kind' ? Object.keys(itemKinds) : field === 'status' ? Object.keys(statuses) : definition?.type === 'Select' ? definition.options.map(option => option.id) : null;
    return [...groups.values()].sort((a, b) => {
        if (!a.key && !b.key) return 0;
        if (!a.key) return 1;
        if (!b.key) return -1;
        if (order) {
            const left = order.indexOf(a.key), right = order.indexOf(b.key);
            if (left !== right) return (left < 0 ? Number.MAX_SAFE_INTEGER : left) - (right < 0 ? Number.MAX_SAFE_INTEGER : right);
        }
        return a.key.localeCompare(b.key, 'de', { numeric: true });
    });
}

const groupIsCollapsed = (view, group, collapsed) => collapsed.has(JSON.stringify([view.id, group.key]));
function groupHeading(view, group, collapsed) {
    const closed = groupIsCollapsed(view, group, collapsed);
    return `<button class="group-heading" data-action="toggle-group" data-group="${escapeHtml(group.key)}" aria-expanded="${!closed}"><span class="group-chevron">${closed ? '▸' : '▾'}</span><span class="group-name">${escapeHtml(group.label)}</span><span class="group-count">${group.tasks.length}</span></button>`;
}

export function renderView(data, view, ui) {
    if (!view) return empty();
    const matching = new Set(data.matchingItemIds);
    const matchingItems = data.items.filter(task => matching.has(task.id));
    const tasks = sortItems(matchingItems.filter(isTask), view, data.customFields).map(task => ({ ...task,
        relatedDocuments: relatedItems(data.items, data.links, task.id).map(relation => relation.item) }));
    const filtered = view.filters.length > 0 || view.hideCompleted || !!data.searchQuery;
    if (!matchingItems.length && view.kind !== 'Timeline')
        return empty(filtered) + (view.kind === 'Tree' && filtered ? quickAdd(view, data.customFields) : '');
    if (view.kind !== 'Tree' && !tasks.length && view.kind !== 'Timeline')
        return '<div class="empty-state"><h3>Keine passenden Aufgaben</h3><p>Ordner und Dokumente findest du in der Baumansicht.</p><button class="secondary-button" data-action="select-kind" data-kind="Tree">Zum gemeinsamen Baum</button></div>';
    switch (view.kind) {
        case 'Kanban': return renderKanban(tasks, view, data.customFields, ui.collapsedGroups);
        case 'Calendar': return renderCalendar(tasks, view, data.customFields, ui.month);
        case 'Timeline': return renderTimeline(tasks, view, data.customFields, ui.timelineDate, ui.timelineInterval, ui.collapsedGroups);
        default: return renderTree(data.items, matching, view, data.customFields, ui.expanded, ui.selectedTaskId, ui.collapsedGroups);
    }
}

function renderTree(allTasks, matching, view, definitions, expanded, selectedId, collapsed) {
    const columns = view.visibleFields.filter(key => key !== 'title' && key !== 'description' && (key.startsWith('field:') || ['kind', 'status', 'startDate', 'dueDate'].includes(key)));
    if (view.visibleFields.includes('description')) columns.push('description');
    const grid = `grid-template-columns:minmax(300px,2.5fr) ${columns.map(() => 'minmax(110px,1fr)').join(' ')} 104px`;
    const rowsFor = (sourceTasks, includeAncestors) => {
        const byId = new Map(sourceTasks.map(task => [task.id, task]));
        const shown = new Set(sourceTasks.filter(task => matching.has(task.id)).map(task => task.id));
        if (includeAncestors) for (const id of matching) {
            let parentId = byId.get(id)?.parentId;
            const visited = new Set();
            while (parentId && byId.has(parentId) && !visited.has(parentId)) {
                visited.add(parentId); shown.add(parentId);
                parentId = byId.get(parentId)?.parentId;
            }
        }
        const children = new Map();
        for (const task of sourceTasks.filter(task => shown.has(task.id))) {
            const parentId = shown.has(task.parentId) ? task.parentId : null;
            if (!children.has(parentId)) children.set(parentId, []);
            children.get(parentId).push(task);
        }
        if (view.sortField) for (const [parentId, list] of children) children.set(parentId, sortItems(list, view, definitions));
        const rows = [];
        const walk = (task, depth) => {
            const subs = children.get(task.id) || [];
            const chevron = subs.length
                ? `<button class="tree-chevron" data-action="toggle" data-id="${task.id}" aria-label="Untergeordnete Einträge ${expanded.has(task.id) ? 'einklappen' : 'ausklappen'}">${expanded.has(task.id) ? '▾' : '▸'}</button>`
                : '<span class="chevron-spacer"></span>';
            rows.push(`<div class="task-row ${isFolder(task) ? 'folder-row' : isDocument(task) ? 'document-row' : ''} ${matching.has(task.id) ? '' : 'context-row'} ${selectedId === task.id ? 'selected' : ''}" style="${grid}" data-action="open-task" data-id="${task.id}" data-depth="${depth}"><div class="tree-title" style="padding-left:${Math.min(depth, 12) * 22}px"><button type="button" class="tree-drag-handle" draggable="true" data-tree-drag="${task.id}" title="Eintrag verschieben" aria-label="${escapeHtml(task.title)} verschieben">⠿</button>${chevron}${itemSymbol(task)}<span class="tree-title-text">${escapeHtml(task.title)}</span></div>${columns.map(key => renderTreeCell(task, key, definitions)).join('')}${rowCreateActions(task)}</div>`);
            if (expanded.has(task.id)) subs.forEach(child => walk(child, depth + 1));
        };
        (children.get(null) || []).forEach(task => walk(task, 0));
        return rows.join('');
    };
    const groups = groupTasks(allTasks.filter(task => matching.has(task.id)), view, definitions);
    const body = groups ? groups.map(group => `<div class="tree-group">${groupHeading(view, group, collapsed)}${groupIsCollapsed(view, group, collapsed) ? '' : rowsFor(group.tasks, false)}</div>`).join('') : rowsFor(allTasks, true);
    return `<div class="table-scroll"><div class="task-table"><div class="table-head" style="${grid}"><span>Eintrag</span>${columns.map(key => `<span>${escapeHtml(fieldLabel(key, definitions))}</span>`).join('')}<span aria-hidden="true"></span></div>${body}</div></div>${quickAdd(view, definitions)}`;
}

function renderTreeCell(task, key, definitions) {
    const field = key.startsWith('field:') && !isFolder(task) ? definitions.find(item => item.id === key.slice(6)) : null;
    if (field) return `<div class="cell-edit">${inlineFieldInput(task, field)}</div>`;
    const due = key === 'dueDate' ? dueState(task) : null;
    return `<div class="cell-muted ${due ? `due-${due}` : ''}"${due ? ` title="${dueTitle[due]}"` : ''}>${escapeHtml(fieldValue(task, key, definitions)) || '—'}</div>`;
}

// Custom fields are edited directly in the tree; app.js saves them on change.
function inlineFieldInput(task, field) {
    const value = task.fields[field.id] || '';
    const pill = field.name === 'Label' && value ? 'inline-pill' : '';
    const attributes = `class="inline-field ${value ? pill : 'empty'}" data-inline-field="${field.id}" data-item-id="${task.id}" aria-label="${escapeHtml(`${field.name} von „${task.title}“`)}"`;
    if (field.type === 'Select' || field.type === 'Boolean') {
        const choices = field.type === 'Boolean' ? [['true', 'Ja'], ['false', 'Nein']] : field.options.map(option => [option.id, option.name]);
        return `<select ${attributes}><option value="">—</option>${choices.map(([key, label]) => `<option value="${escapeHtml(key)}" ${key === value ? 'selected' : ''}>${escapeHtml(label)}</option>`).join('')}</select>`;
    }
    const type = { Number: 'number', Date: 'date' }[field.type] || 'text';
    return `<input ${attributes} type="${type}" ${type === 'number' ? 'step="any"' : ''} maxlength="500" placeholder="—" value="${escapeHtml(value)}" title="${escapeHtml(value)}" />`;
}

function renderKanbanBoard(tasks, view, definitions, groupKey, columnStatuses = Object.keys(statuses)) {
    const groupAttribute = groupKey === null ? '' : ` data-group="${escapeHtml(groupKey)}"`;
    return `<div class="kanban-board ${columnStatuses.length === 1 ? 'single-status' : ''}">${columnStatuses.map(status => {
        const label = statuses[status];
        const columnTasks = tasks.filter(task => task.status === status);
        return `<section class="kanban-column" data-status="${status}"${groupAttribute}><div class="column-heading"><span class="status-dot ${status}"></span>${label}<span class="column-count">${columnTasks.length}</span></div><div class="column-body">${columnTasks.map(task => `<article class="kanban-card" draggable="true" data-action="open-task" data-id="${task.id}"${groupAttribute}><div class="card-title">${escapeHtml(task.title)}</div>${view.visibleFields.includes('description') && task.description ? `<p class="card-description">${escapeHtml(descriptionText(task.description))}</p>` : ''}<div class="card-meta">${view.visibleFields.filter(key => !['title', 'status', 'description'].includes(key)).map(key => fieldValue(task, key, definitions) ? `<span${key === 'dueDate' && dueState(task) ? ` class="due-${dueState(task)}"` : ''}>${escapeHtml(fieldLabel(key, definitions))}: ${escapeHtml(fieldValue(task, key, definitions))}</span>` : '').join('')}</div>${documentRefs(task)}</article>`).join('')}${groupKey === null || view.groupByField === 'status' ? `<button class="add-column-task" data-action="new-task" data-status="${status}">＋ Aufgabe hinzufügen</button>` : ''}</div></section>`;
    }).join('')}</div>`;
}

function renderKanban(tasks, view, definitions, collapsed) {
    let groups = groupTasks(tasks, view, definitions);
    if (!groups) return renderKanbanBoard(tasks, view, definitions, null);
    if (view.groupByField === 'status') {
        const byStatus = new Map(groups.map(group => [group.key, group]));
        groups = Object.entries(statuses).map(([key, label]) => byStatus.get(key) || { key, label, tasks: [] });
    }
    return `<div class="kanban-groups">${groups.map(group => `<section class="kanban-group">${groupHeading(view, group, collapsed)}${groupIsCollapsed(view, group, collapsed) ? '' : renderKanbanBoard(group.tasks, view, definitions, group.key, view.groupByField === 'status' ? [group.key] : undefined)}</section>`).join('')}</div>`;
}

function renderCalendar(tasks, view, definitions, monthValue) {
    const [year, month] = monthValue.split('-').map(Number);
    const first = new Date(Date.UTC(year, month - 1, 1));
    const offset = (first.getUTCDay() + 6) % 7;
    const start = new Date(Date.UTC(year, month - 1, 1 - offset));
    const dayMs = 86400000;
    const scheduled = tasks.map(task => {
        const firstDay = task.startDate || task.dueDate;
        const lastDay = task.dueDate || task.startDate;
        if (!firstDay || !lastDay) return null;
        const from = Date.parse(`${firstDay}T00:00:00Z`);
        const to = Date.parse(`${lastDay}T00:00:00Z`);
        return Number.isFinite(from) && Number.isFinite(to)
            ? { task, from: Math.min(from, to), to: Math.max(from, to) }
            : null;
    }).filter(Boolean);
    const todayDate = new Date();
    const today = `${todayDate.getFullYear()}-${String(todayDate.getMonth() + 1).padStart(2, '0')}-${String(todayDate.getDate()).padStart(2, '0')}`;
    const weeks = Array.from({ length: 6 }, (_, weekIndex) => {
        const weekStart = start.getTime() + weekIndex * 7 * dayMs;
        const weekEnd = weekStart + 6 * dayMs;
        const days = Array.from({ length: 7 }, (_, dayIndex) => {
            const date = new Date(weekStart + dayIndex * dayMs);
            const iso = date.toISOString().slice(0, 10);
            return `<div class="calendar-day ${date.getUTCMonth() !== month - 1 ? 'outside' : ''} ${iso === today ? 'today' : ''}"><span class="day-number">${date.getUTCDate()}</span></div>`;
        }).join('');
        const events = scheduled.filter(item => item.from <= weekEnd && item.to >= weekStart)
            .map(item => ({ ...item, column: Math.max(0, (item.from - weekStart) / dayMs),
                lastColumn: Math.min(6, (item.to - weekStart) / dayMs) }))
            .sort((a, b) => a.column - b.column || b.lastColumn - a.lastColumn || a.task.title.localeCompare(b.task.title));
        const laneEnds = [];
        const bars = events.map(item => {
            let lane = laneEnds.findIndex(last => last < item.column);
            if (lane < 0) lane = laneEnds.length;
            laneEnds[lane] = item.lastColumn;
            const details = view.visibleFields.filter(key => !['title', 'startDate', 'dueDate'].includes(key))
                .map(key => fieldValue(item.task, key, definitions)).filter(Boolean);
            if (item.task.relatedDocuments.length) details.push(`📄 ${item.task.relatedDocuments.length}`);
            const tooltip = [item.task.title, `${formatDate(new Date(item.from).toISOString().slice(0, 10))} – ${formatDate(new Date(item.to).toISOString().slice(0, 10))}`, ...details,
                ...item.task.relatedDocuments.map(doc => `Dokument: ${doc.title}`)].join(' · ');
            return `<button class="calendar-event ${dueState(item.task) ? `due-${dueState(item.task)}` : ''} ${item.from < weekStart ? 'continues-before' : ''} ${item.to > weekEnd ? 'continues-after' : ''}" style="grid-column:${item.column + 1}/span ${item.lastColumn - item.column + 1};grid-row:${lane + 1}" data-action="open-task" data-id="${item.task.id}" title="${escapeHtml(tooltip)}"><strong>${escapeHtml(item.task.title)}</strong>${details.length ? `<small>${escapeHtml(details.join(' · '))}</small>` : ''}</button>`;
        }).join('');
        return `<div class="calendar-week" style="min-height:${Math.max(118, 42 + laneEnds.length * 45)}px">${days}<div class="calendar-week-events" style="grid-template-rows:repeat(${laneEnds.length},40px)">${bars}</div></div>`;
    }).join('');
    const unscheduled = tasks.filter(task => !task.dueDate && !task.startDate);
    return `<div class="calendar-wrap"><div class="date-nav"><strong>${new Intl.DateTimeFormat('de-DE', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(first)}</strong><button data-action="calendar-prev" aria-label="Vorheriger Monat">‹</button><button data-action="calendar-next" aria-label="Nächster Monat">›</button></div><div class="calendar-grid"><div class="calendar-weekday-row">${['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So'].map(day => `<div class="weekday">${day}</div>`).join('')}</div>${weeks}</div>${unscheduled.length ? `<div class="unscheduled"><strong>Ohne Datum</strong>${unscheduled.map(task => `<button data-action="open-task" data-id="${task.id}">${escapeHtml(task.title)}</button>`).join('')}</div>` : ''}</div>`;
}

function renderTimeline(tasks, view, definitions, anchor, interval, collapsed) {
    const range = getTimelineRange(anchor, interval);
    const start = new Date(`${range.start}T00:00:00Z`);
    const end = new Date(`${range.end}T00:00:00Z`);
    const headings = range.columns.map(column => `<span>${escapeHtml(column.label)}</span>`).join('');
    const renderRows = items => items.map(task => {
        const taskStart = task.startDate ? new Date(`${task.startDate}T00:00:00Z`) : task.dueDate ? new Date(`${task.dueDate}T00:00:00Z`) : null;
        const taskEnd = task.dueDate ? new Date(`${task.dueDate}T00:00:00Z`) : taskStart;
        let bar = '<span class="timeline-undated">Kein Datum</span>';
        if (taskStart && taskEnd) {
            const left = Math.max(0, (taskStart - start) / 86400000);
            const right = Math.min(range.days, (taskEnd - start) / 86400000 + 1);
            const tooltip = escapeHtml(`${task.title} · ${formatDate(task.startDate || task.dueDate)} – ${formatDate(task.dueDate || task.startDate)}`);
            bar = right > left ? `<div class="timeline-bar ${task.status} ${dueState(task) ? `due-${dueState(task)}` : ''}" style="left:${left / range.days * 100}%;width:${Math.max((right - left) / range.days * 100, 1.5)}%" data-id="${task.id}" title="${tooltip}">${taskStart >= start ? '<button type="button" class="timeline-resize start" data-edge="start" title="Startdatum ändern" aria-label="Startdatum ändern"></button>' : ''}<button type="button" class="timeline-body" data-action="open-task" data-id="${task.id}">${escapeHtml(task.title)}</button>${taskEnd < end ? '<button type="button" class="timeline-resize end" data-edge="end" title="Enddatum ändern" aria-label="Enddatum ändern"></button>' : ''}</div>` : '';
        }
        const extras = view.visibleFields.filter(key => key !== 'title').map(key => fieldValue(task, key, definitions)).filter(Boolean);
        if (task.relatedDocuments.length) extras.unshift(`📄 ${task.relatedDocuments.length} ${task.relatedDocuments.length === 1 ? 'Dokument' : 'Dokumente'}`);
        return `<div class="timeline-row"><div class="timeline-label" data-action="open-task" data-id="${task.id}" title="${escapeHtml(task.title)}">${escapeHtml(task.title)}${extras.length ? `<span class="cell-muted"> · ${escapeHtml(extras.join(', '))}</span>` : ''}</div><div class="timeline-track">${bar}</div></div>`;
    }).join('');
    const groups = groupTasks(tasks, view, definitions);
    const body = !tasks.length ? empty(view.filters.length > 0 || view.hideCompleted) : groups ? groups.map(group => `<div class="timeline-group-heading">${groupHeading(view, group, collapsed)}</div>${groupIsCollapsed(view, group, collapsed) ? '' : renderRows(group.tasks)}`).join('') : renderRows(tasks);
    const endLabel = new Date(end); endLabel.setUTCDate(endLabel.getUTCDate() - 1);
    const intervalButtons = Object.entries(timelineIntervals).map(([key, label]) => `<button type="button" class="timeline-interval-button ${key === interval ? 'active' : ''}" data-action="timeline-interval" data-interval="${key}" aria-pressed="${key === interval}">${label}</button>`).join('');
    return `<div class="timeline-wrap" data-start="${range.start}" data-days="${range.days}"><div class="date-nav timeline-nav"><strong>${formatDate(range.start)} – ${formatDate(endLabel.toISOString().slice(0, 10))}</strong><button data-action="timeline-prev" aria-label="Vorheriger Zeitraum">‹</button><button data-action="timeline-next" aria-label="Nächster Zeitraum">›</button><div class="timeline-intervals" role="group" aria-label="Zeitintervall">${intervalButtons}</div></div><div class="timeline-grid" style="--timeline-columns:${range.columns.map(column => `${column.days}fr`).join(' ')};--tick-width:${100 / range.days * (interval === 'ThreeMonths' ? 7 : 1)}%"><div class="timeline-heading"><span>Aufgabe</span><div class="timeline-weeks ${interval === 'Month' ? 'timeline-day-headings' : ''}">${headings}</div></div>${body}</div></div>`;
}
