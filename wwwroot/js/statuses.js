// The task statuses of the project, ordered by category and position. app.js sets them after every load.
// Every status belongs to a fixed category; the app only looks at the category, e.g. for "completed".
import { api } from './api.js?v=problem-details';
import { projectPath } from './project.js';

export const statusCategories = { Open: 'Offen', Active: 'In Arbeit', Done: 'Abgeschlossen' };
export const statusColors = { gray: 'Grau', slate: 'Schiefer', blue: 'Blau', teal: 'Petrol', green: 'Grün', orange: 'Orange', purple: 'Lila', pink: 'Pink' };

// A status filter value is a status id, or this prefix and a category ("category:Done").
export const categoryFilterPrefix = 'category:';
export const categoryFilterLabels = { Open: 'Alle offenen', Active: 'Alle in Arbeit', Done: 'Alle abgeschlossenen' };

let list = [];
export function setStatuses(statuses) { list = statuses; }
export const allStatuses = () => list;
export const statusById = id => list.find(status => status.id === id) || null;
export const statusName = id => statusById(id)?.name ?? '';
export const statusCategory = item => statusById(item?.statusId)?.category ?? null;
export const isCompleted = item => statusCategory(item) === 'Done';
export const firstStatusId = category => list.find(status => status.category === category)?.id ?? null;
// New tasks start with the first open status.
export const defaultStatusId = () => firstStatusId('Open');

// A colour that no status uses yet, so a new column stands out.
export const unusedColor = () => Object.keys(statusColors).find(color => list.every(status => status.color !== color)) || 'gray';

// A name that no status uses yet, e.g. "Neuer Status 2".
export function unusedName(base = 'Neuer Status') {
    const taken = new Set(list.map(status => status.name.toLocaleLowerCase('de')));
    for (let number = 1; ; number++) {
        const name = number === 1 ? base : `${base} ${number}`;
        if (!taken.has(name.toLocaleLowerCase('de'))) return name;
    }
}

// Saves the complete list in display order ({ id, name, category, color }, id null for new statuses).
// Tasks of removed statuses move to replacements[removedId], by default the first remaining status of the same category.
export const saveStatusList = (statuses, replacements = {}) => api(projectPath('/statuses'), { method: 'PUT', body: JSON.stringify({
    statuses: statuses.map(({ id, name, category, color }) => ({ id: id || null, name, category, color })), replacements }) });

// The status a filter value stands for when a new entry is prefilled from it.
export const statusForFilterValue = value => value.startsWith(categoryFilterPrefix)
    ? firstStatusId(value.slice(categoryFilterPrefix.length)) : statusById(value)?.id ?? null;
