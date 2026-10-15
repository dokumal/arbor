import { api } from './api.js?v=problem-details';
import { escapeHtml } from './render.js?v=quick-add';
import {
    addDays, apiTime, dayMinutes, entryMinutes, formatDuration, formatTime, localToday, mondayOf, snap, snapMinutes,
    toMinutes, visibleRange, layoutDay, weekLabel, weekdayIndex, workdayEnd, workdayStart
} from './time-week.js';

const $ = id => document.getElementById(id);
const weekdays = ['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So'];
const weekdayNames = ['Montag', 'Dienstag', 'Mittwoch', 'Donnerstag', 'Freitag', 'Samstag', 'Sonntag'];
const requestedWeek = new URLSearchParams(location.search).get('woche');
const state = {
    monday: mondayOf(/^\d{4}-\d{2}-\d{2}$/.test(requestedWeek || '') && !isNaN(Date.parse(requestedWeek)) ? requestedWeek : localToday()),
    entries: [], range: { start: workdayStart, end: workdayEnd },
    showWeekend: localStorage.getItem('arbor-time-weekend') === 'true',
    editingId: null
};
let loadCounter = 0;
let drag = null;
let busy = false;
let suppressClick = false;
let toastTimer;

const entryById = id => state.entries.find(entry => entry.id === id);
const sumMinutes = entries => entries.reduce((total, entry) => total + entryMinutes(entry), 0);

async function load() {
    const request = ++loadCounter;
    const query = `?from=${state.monday}&to=${addDays(state.monday, 6)}`;
    history.replaceState(null, '', state.monday === mondayOf(localToday()) ? location.pathname : `${location.pathname}?woche=${state.monday}`);
    $('week-label').textContent = weekLabel(state.monday);
    try {
        const entries = await api(`/api/time-entries${query}`);
        if (request !== loadCounter) return;
        state.entries = entries;
        render();
    } catch (error) {
        if (request !== loadCounter) return;
        $('week-view').innerHTML = `<div class="empty-state"><h3>Die Woche konnte nicht geladen werden</h3><p>${escapeHtml(error.message)}</p></div>`;
    }
}

// Weekend days are shown on request and whenever they hold entries, so no entry is ever hidden.
function visibleDays() {
    const days = [];
    for (let index = 0; index < 7; index++) {
        const date = addDays(state.monday, index);
        if (index < 5 || state.showWeekend || state.entries.some(entry => entry.date === date)) days.push(date);
    }
    return days;
}

const percent = minutes => `${(minutes - state.range.start) / (state.range.end - state.range.start) * 100}%`;
const span = (start, end) => `${(end - start) / (state.range.end - state.range.start) * 100}%`;

function entryHtml({ entry, start, end, column, columns }) {
    const time = `${formatTime(start)} – ${formatTime(end)}`;
    const compact = end - start < 45 ? ' compact' : '';
    return `<button type="button" class="time-entry${compact}" data-id="${entry.id}" title="${escapeHtml(`${time} · ${formatDuration(end - start)}\n${entry.description}`)}"
        style="top:${percent(start)};height:${span(start, end)};left:${column / columns * 100}%;width:${100 / columns}%">
        <span class="time-entry-handle start" data-edge="start" aria-hidden="true"></span>
        <span class="time-entry-time">${time}</span><span class="time-entry-text">${escapeHtml(entry.description)}</span>
        <span class="time-entry-handle end" data-edge="end" aria-hidden="true"></span></button>`;
}

function render() {
    const days = visibleDays();
    const today = localToday();
    state.range = visibleRange(state.entries);
    const { start, end } = state.range;
    const hours = (end - start) / 60;
    const offHours = [[start, workdayStart], [workdayEnd, end]].filter(([from, to]) => to > from)
        .map(([from, to]) => `<div class="week-off-hours" style="top:${percent(from)};height:${span(from, to)}"></div>`).join('');
    const labels = Array.from({ length: hours }, (_, index) => start + index * 60)
        .map(minutes => `<span class="week-hour" style="top:${percent(minutes)}">${formatTime(minutes)}</span>`).join('');
    const total = sumMinutes(state.entries);
    $('week-total').textContent = `${formatDuration(total)} diese Woche`;
    $('show-weekend').checked = state.showWeekend;

    $('week-view').innerHTML = `<div class="week-grid" style="--days:${days.length};--hours:${hours}">
        <div class="week-head"><div class="week-corner">${formatDuration(total)}</div>${days.map(date => {
            const index = weekdayIndex(date);
            const dayTotal = sumMinutes(state.entries.filter(entry => entry.date === date));
            return `<div class="week-day-head${date === today ? ' today' : ''}${index > 4 ? ' weekend' : ''}">
                <span class="week-day-name">${weekdays[index]}</span><span class="week-day-date">${Number(date.slice(8))}.${Number(date.slice(5, 7))}.</span>
                <span class="week-day-total">${dayTotal ? formatDuration(dayTotal) : ''}</span></div>`;
        }).join('')}</div>
        <div class="week-body"><div class="week-hours" aria-hidden="true">${labels}</div>${days.map(date => {
            const index = weekdayIndex(date);
            const entries = layoutDay(state.entries.filter(entry => entry.date === date));
            return `<div class="week-day${date === today ? ' today' : ''}${index > 4 ? ' weekend' : ''}" data-date="${date}" role="group" aria-label="${weekdayNames[index]}, ${Number(date.slice(8))}.${Number(date.slice(5, 7))}.">
                ${offHours}${entries.map(entryHtml).join('')}${date === today ? '<div class="week-now" aria-hidden="true"></div>' : ''}</div>`;
        }).join('')}</div></div>`;
    placeNowLine();
}

function placeNowLine() {
    const line = $('week-view').querySelector('.week-now');
    if (!line) return;
    const now = new Date();
    const minutes = now.getHours() * 60 + now.getMinutes();
    line.hidden = minutes < state.range.start || minutes > state.range.end;
    line.style.top = percent(minutes);
}
setInterval(() => {
    if (drag) return;
    // After midnight the highlighted day moves on.
    if ($('week-view').querySelector('.week-day.today')?.dataset.date !== localToday()) render();
    else placeNowLine();
}, 60000);

function notify(message) {
    clearTimeout(toastTimer);
    $('toast').textContent = message;
    $('toast').hidden = false;
    toastTimer = setTimeout(() => $('toast').hidden = true, 3000);
}

// Minutes since midnight at a vertical position inside a day column, limited to the visible range.
function minutesAt(column, y) {
    const rect = column.getBoundingClientRect();
    const minutes = state.range.start + (y - rect.top) / rect.height * (state.range.end - state.range.start);
    return Math.max(state.range.start, Math.min(state.range.end, minutes));
}

const columnAt = x => [...$('week-view').querySelectorAll('.week-day')].find(column => {
    const rect = column.getBoundingClientRect();
    return x >= rect.left && x < rect.right;
});

function showSelection(column, start, end) {
    let selection = $('week-view').querySelector('.time-selection');
    if (!selection) {
        selection = document.createElement('div');
        selection.className = 'time-selection';
        selection.setAttribute('aria-hidden', 'true');
    }
    if (selection.parentElement !== column) column.append(selection);
    selection.style.top = percent(start);
    selection.style.height = span(start, end);
    selection.textContent = `${formatTime(start)} – ${formatTime(end)}`;
}
const clearSelection = () => $('week-view').querySelector('.time-selection')?.remove();

// --- Dialog ---

// The time inputs take 5-minute steps, so other times (such as the end of the day, stored as 23:59) are rounded onto that grid.
const inputStep = 5;
const inputTime = time => formatTime(Math.min(Math.round(toMinutes(time) / inputStep) * inputStep, dayMinutes - inputStep));

function updateDuration() {
    const start = $('entry-start').value, end = $('entry-end').value;
    $('entry-duration').textContent = start && end && toMinutes(end) > toMinutes(start)
        ? `Dauer: ${formatDuration(toMinutes(end) - toMinutes(start))}` : '';
}

function openDialog(entry) {
    state.editingId = entry.id || null;
    $('entry-dialog-heading').textContent = entry.id ? 'Aufwand bearbeiten' : 'Aufwand erfassen';
    $('entry-date').value = entry.date;
    $('entry-start').value = inputTime(entry.start);
    $('entry-end').value = inputTime(entry.end);
    $('entry-description').value = entry.description || '';
    $('delete-entry-button').hidden = !entry.id;
    $('entry-form-error').textContent = '';
    updateDuration();
    $('entry-dialog').showModal();
    $('entry-description').focus();
}

// A new entry starts where the day's last entry ends, or at the start of the working day.
function openNewEntry() {
    const today = localToday();
    const date = mondayOf(today) === state.monday ? today : state.monday;
    const ends = state.entries.filter(entry => entry.date === date).map(entry => toMinutes(entry.end));
    const start = Math.min(ends.length ? Math.max(...ends) : workdayStart, dayMinutes - snapMinutes);
    openDialog({ date, start: formatTime(start), end: formatTime(Math.min(start + 60, dayMinutes - 1)) });
}

$('entry-dialog').addEventListener('close', () => { clearSelection(); state.editingId = null; });
document.querySelectorAll('#entry-dialog .dialog-close').forEach(button => button.addEventListener('click', () => $('entry-dialog').close()));
$('entry-start').addEventListener('input', updateDuration);
$('entry-end').addEventListener('input', updateDuration);

$('entry-form').addEventListener('submit', async event => {
    event.preventDefault();
    const start = $('entry-start').value, end = $('entry-end').value;
    if (toMinutes(end) <= toMinutes(start)) {
        $('entry-form-error').textContent = 'Das Ende muss nach dem Beginn liegen.';
        return;
    }
    const payload = { date: $('entry-date').value, start: `${start.slice(0, 5)}:00`, end: `${end.slice(0, 5)}:00`, description: $('entry-description').value };
    const id = state.editingId;
    $('entry-form-error').textContent = '';
    try {
        const saved = await api(id ? `/api/time-entries/${id}` : '/api/time-entries', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload) });
        $('entry-dialog').close();
        // An entry moved to another week is shown there.
        state.monday = mondayOf(saved.date);
        await load();
        notify(id ? 'Aufwand gespeichert.' : 'Aufwand erfasst.');
    } catch (error) { $('entry-form-error').textContent = error.message; }
});

$('delete-entry-button').addEventListener('click', async () => {
    const id = state.editingId;
    if (!id || !confirm('Diesen Aufwand löschen?')) return;
    try {
        await api(`/api/time-entries/${id}`, { method: 'DELETE' });
        $('entry-dialog').close();
        await load();
        notify('Aufwand gelöscht.');
    } catch (error) { $('entry-form-error').textContent = error.message; }
});

// --- Pointer: drag in an empty area to create, drag an entry to move it, drag its edges to resize ---

$('week-view').addEventListener('pointerdown', event => {
    if (busy || drag || event.button !== 0 || event.isPrimary === false) return;
    const element = event.target.closest('.time-entry');
    const column = event.target.closest('.week-day');
    if (!column) return;
    const base = { pointerId: event.pointerId, x: event.clientX, y: event.clientY, moved: false };
    if (!element) {
        drag = { ...base, kind: 'create', column, anchor: minutesAt(column, event.clientY) };
        event.preventDefault();
        return;
    }
    const entry = entryById(element.dataset.id);
    if (!entry) return;
    drag = {
        ...base, kind: event.target.closest('.time-entry-handle')?.dataset.edge || 'move', element, entry, column,
        start: toMinutes(entry.start), end: toMinutes(entry.end), date: entry.date,
        pixelsPerMinute: column.getBoundingClientRect().height / (state.range.end - state.range.start)
    };
});

document.addEventListener('pointermove', event => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    if (!drag.moved && Math.hypot(event.clientX - drag.x, event.clientY - drag.y) < 4) return;
    drag.moved = true;
    event.preventDefault();
    if (drag.kind === 'create') {
        const from = Math.min(drag.anchor, minutesAt(drag.column, event.clientY));
        const to = Math.max(drag.anchor, minutesAt(drag.column, event.clientY));
        drag.selection = {
            start: Math.max(state.range.start, Math.floor(from / snapMinutes) * snapMinutes),
            end: Math.min(state.range.end, Math.ceil(to / snapMinutes) * snapMinutes)
        };
        if (drag.selection.end === drag.selection.start) drag.selection.end = Math.min(state.range.end, drag.selection.start + snapMinutes);
        if (drag.selection.end === drag.selection.start) drag.selection.start -= snapMinutes;
        showSelection(drag.column, drag.selection.start, drag.selection.end);
        return;
    }
    const original = { start: toMinutes(drag.entry.start), end: toMinutes(drag.entry.end) };
    const delta = (event.clientY - drag.y) / drag.pixelsPerMinute;
    const { start: min, end: max } = state.range;
    if (drag.kind === 'move') {
        const duration = original.end - original.start;
        drag.start = Math.max(min, Math.min(max - duration, snap(original.start + delta)));
        drag.end = drag.start + duration;
        const column = columnAt(event.clientX);
        if (column && column !== drag.column) { drag.column = column; drag.date = column.dataset.date; }
    } else if (drag.kind === 'start') {
        drag.start = Math.max(min, Math.min(original.end - snapMinutes, snap(original.start + delta)));
    } else {
        drag.end = Math.min(max, Math.max(original.start + snapMinutes, snap(original.end + delta)));
    }
    const { element } = drag;
    if (element.parentElement !== drag.column) drag.column.append(element);
    element.classList.add('dragging');
    element.style.top = percent(drag.start);
    element.style.height = span(drag.start, drag.end);
    element.style.left = '0%';
    element.style.width = '100%';
    element.querySelector('.time-entry-time').textContent = `${formatTime(drag.start)} – ${formatTime(drag.end)}`;
}, { passive: false });

document.addEventListener('pointerup', async event => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    const current = drag; drag = null;
    if (current.kind === 'create') {
        let selection = current.selection;
        // A click selects the half hour under the pointer.
        if (!current.moved) {
            const start = Math.min(Math.floor(current.anchor / 30) * 30, state.range.end - 30);
            selection = { start, end: start + 30 };
            showSelection(current.column, selection.start, selection.end);
        }
        openDialog({ date: current.column.dataset.date, start: formatTime(selection.start), end: formatTime(Math.min(selection.end, dayMinutes - 1)) });
        return;
    }
    if (!current.moved) return;
    suppressClick = true;
    setTimeout(() => { suppressClick = false; }, 0);
    const { entry } = current;
    if (current.date === entry.date && current.start === toMinutes(entry.start) && current.end === toMinutes(entry.end)) {
        render(); return;
    }
    busy = true;
    current.element.classList.add('saving');
    try {
        await api(`/api/time-entries/${entry.id}`, { method: 'PUT', body: JSON.stringify({
            date: current.date, start: apiTime(current.start), end: apiTime(current.end), description: entry.description }) });
        await load();
    } catch (error) {
        render();
        notify(error.message);
    } finally { busy = false; }
});

function cancelDrag() {
    if (!drag) return;
    const current = drag; drag = null;
    if (current.kind === 'create') clearSelection();
    else if (current.moved) render();
}
document.addEventListener('pointercancel', event => { if (drag?.pointerId === event.pointerId) cancelDrag(); });
window.addEventListener('blur', cancelDrag);
document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && drag) { event.preventDefault(); cancelDrag(); }
});

$('week-view').addEventListener('click', event => {
    const element = event.target.closest('.time-entry');
    if (!element) return;
    if (suppressClick) { event.preventDefault(); return; }
    const entry = entryById(element.dataset.id);
    if (entry) openDialog(entry);
});

// --- Week navigation ---

function goToWeek(monday) {
    if (drag) return;
    state.monday = monday;
    load();
}
$('previous-week').addEventListener('click', () => goToWeek(addDays(state.monday, -7)));
$('next-week').addEventListener('click', () => goToWeek(addDays(state.monday, 7)));
$('current-week').addEventListener('click', () => goToWeek(mondayOf(localToday())));
$('new-entry-button').addEventListener('click', openNewEntry);
$('show-weekend').addEventListener('change', event => {
    state.showWeekend = event.target.checked;
    localStorage.setItem('arbor-time-weekend', String(state.showWeekend));
    render();
});
document.addEventListener('keydown', event => {
    if (event.altKey || event.ctrlKey || event.metaKey || $('entry-dialog').open) return;
    if (event.target.closest('input, textarea, select, [contenteditable]')) return;
    if (event.key === 'ArrowLeft') goToWeek(addDays(state.monday, -7));
    else if (event.key === 'ArrowRight') goToWeek(addDays(state.monday, 7));
});

load();
