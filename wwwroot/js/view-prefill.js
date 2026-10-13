// Values for a new entry so that it already meets the filters of a view.
// Only filters that name a value are used ("ist", "enthält", "vor", "nach"); the rest leave the field empty.

const dayMs = 86400000;
const localDate = (offset = 0) => {
    const now = new Date();
    return new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()) + offset * dayMs).toISOString().slice(0, 10);
};
const shiftDate = (date, days) => new Date(Date.parse(`${date}T00:00:00Z`) + days * dayMs).toISOString().slice(0, 10);

// Same relative values as the server: "@today", "@today+7", "@today-3".
function resolveDate(value) {
    const match = /^@today(?:([+-])(\d{1,4}))?$/.exec(value.trim());
    if (!match) return value.trim();
    const days = Number(match[2] || 0);
    return localDate(match[1] === '-' ? -days : days);
}

// Today when it fits, otherwise the closest day that does.
function dateFor(operator, value) {
    const date = resolveDate(value);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return null;
    const today = localDate();
    if (operator === 'equals') return date;
    if (operator === 'before') return today < date ? today : shiftDate(date, -1);
    if (operator === 'after') return today > date ? today : shiftDate(date, 1);
    return null;
}

// Status and dates only exist on tasks, custom fields not on folders.
const taskOnlyFields = ['status', 'startDate', 'dueDate'];

// The kinds of entry that can meet all filters. Folders are only offered when a view asks for them.
function possibleKinds(filters) {
    let kinds = ['Task', 'Document', 'Folder'];
    for (const { field: key, operator, value } of filters) {
        const needsValue = !['isEmpty', 'notEquals'].includes(operator);
        if (key === 'kind' && operator === 'equals') kinds = kinds.filter(kind => kind === value);
        else if (key === 'kind' && operator === 'notEquals') kinds = kinds.filter(kind => kind !== value);
        else if (key === 'status' && operator === 'isEmpty') kinds = kinds.filter(kind => kind !== 'Task');
        else if (taskOnlyFields.includes(key) && needsValue) kinds = kinds.filter(kind => kind === 'Task');
        else if (key.startsWith('field:') && needsValue) kinds = kinds.filter(kind => kind !== 'Folder');
    }
    const asksForFolders = filters.some(filter => filter.field === 'kind' && filter.operator === 'equals' && filter.value === 'Folder');
    return kinds.filter(kind => kind !== 'Folder' || asksForFolders);
}

export function viewPrefill(view, definitions) {
    const filters = view?.filters || [];
    const prefill = { kinds: possibleKinds(filters), title: '', status: null, startDate: null, dueDate: null, fields: {}, keys: [] };
    const set = (key, apply) => { apply(); if (!prefill.keys.includes(key)) prefill.keys.push(key); };
    for (const { field: key, operator, value } of filters) {
        if (!value.trim()) continue;
        if (key === 'title' && ['equals', 'contains'].includes(operator)) set(key, () => { prefill.title = value.trim(); });
        else if (key === 'status' && operator === 'equals') set(key, () => { prefill.status = value; });
        else if (key === 'startDate' || key === 'dueDate') {
            const date = dateFor(operator, value);
            if (date) set(key, () => { prefill[key] = date; });
        } else if (key.startsWith('field:')) {
            const field = definitions.find(item => item.id === key.slice(6));
            if (!field) continue;
            const fieldValue = field.type === 'Date' ? dateFor(operator, value)
                : operator === 'equals' || (operator === 'contains' && field.type === 'Text') ? value.trim() : null;
            if (fieldValue) set(key, () => { prefill.fields[field.id] = fieldValue; });
        }
    }
    // A start after the due date would be rejected; the due date wins.
    if (prefill.startDate && prefill.dueDate && prefill.startDate > prefill.dueDate) {
        prefill.startDate = null;
        prefill.keys = prefill.keys.filter(key => key !== 'startDate');
    }
    return prefill;
}

// The prefilled values as an entry of the given kind; documents and folders have no status or dates.
export function prefilledItem(prefill, kind) {
    const task = kind === 'Task';
    return {
        kind, title: prefill.title, status: task && prefill.status || 'Todo',
        startDate: task ? prefill.startDate : null, dueDate: task ? prefill.dueDate : null,
        fields: kind === 'Folder' ? {} : { ...prefill.fields }
    };
}

// The keys of the fields that are prefilled for the given kind.
export const prefilledKeys = (prefill, kind) => prefill.keys.filter(key =>
    kind === 'Task' || key === 'title' || (kind === 'Document' && !taskOnlyFields.includes(key)));
