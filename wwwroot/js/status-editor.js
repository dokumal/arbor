// Editable list of task statuses, grouped by category, in the custom field dialog.
// Rows keep their status id so a rename can be told apart from a new status. Rows move by dragging the handle
// or with the arrow keys on it, also into another category. A removed status that tasks still use stays visible
// until saving, so the user can pick where its tasks go.
import { escapeHtml, statusDot } from './render.js?v=statuses';
import { statusCategories, statusColors } from './statuses.js';

const swatches = selected => Object.entries(statusColors).map(([color, label]) =>
    `<button type="button" class="status-swatch status-color-${color}" data-status-color="${color}" aria-pressed="${color === selected}" title="${label}" aria-label="Farbe ${label}"></button>`).join('');

function statusRow(status, usage) {
    const count = status.id ? usage.get(status.id) || 0 : 0;
    return `<div class="status-row" data-status-id="${escapeHtml(status.id || '')}" data-color="${escapeHtml(status.color)}" data-in-use="${count}">
        <div class="status-row-main"><button type="button" class="status-drag-handle" draggable="true" title="Verschieben (ziehen oder Pfeiltasten)" aria-label="„${escapeHtml(status.name)}“ verschieben">⠿</button><button type="button" class="status-color-button" data-status-color-toggle aria-expanded="false" title="Farbe ändern" aria-label="Farbe von „${escapeHtml(status.name)}“ ändern">${statusDot(status)}</button><input class="status-name" value="${escapeHtml(status.name)}" maxlength="50" aria-label="Statusname" /><span class="status-default-badge" hidden>Standard</span><span class="status-usage">${count ? `${count} ${count === 1 ? 'Aufgabe' : 'Aufgaben'}` : ''}</span><button type="button" class="option-remove" data-status-remove title="Status entfernen" aria-label="Status „${escapeHtml(status.name)}“ entfernen">×</button></div>
        <div class="status-swatches" role="group" aria-label="Farbe" hidden>${swatches(status.color)}</div>
        <div class="status-removed" hidden><label>${count} ${count === 1 ? 'Aufgabe' : 'Aufgaben'} verschieben nach <select class="status-replacement" aria-label="Neuer Status für die Aufgaben"></select></label><button type="button" class="text-button" data-status-restore>Rückgängig</button></div>
    </div>`;
}

// usage maps a status id to the number of tasks with that status.
export function renderStatusEditor(statuses, usage) {
    const categories = Object.entries(statusCategories).map(([category, label]) => `<section class="status-category" data-category="${category}">
        <div class="status-category-heading">${statusDot({ category, color: 'gray' })}<span>${label}</span></div>
        <div class="status-list" data-category="${category}">${statuses.filter(status => status.category === category).map(status => statusRow(status, usage)).join('')}</div>
        <div class="option-add"><input class="status-new" maxlength="50" placeholder="Neuer Status …" aria-label="Neuer Status in „${label}“" /><button type="button" class="secondary-button" data-status-add>＋ Hinzufügen</button></div>
    </section>`).join('');
    return `<div class="status-editor"><p class="helper-text">Die Kategorie bestimmt, wie ein Status behandelt wird: Abgeschlossene Aufgaben gelten als erledigt und sind nie überfällig. Neue Aufgaben erhalten den ersten offenen Status.</p>${categories}<p class="option-error form-error" role="alert" aria-live="polite"></p></div>`;
}

const rows = editor => [...editor.querySelectorAll('.status-row')];
const isRemoved = row => row.classList.contains('removed');
const rowName = row => row.querySelector('.status-name').value.trim();
const rowCategory = row => row.closest('.status-list').dataset.category;

// Keeps dots, the default badge and the replacement choices in step with the rows.
function refresh(editor) {
    const kept = rows(editor).filter(row => !isRemoved(row));
    const defaultRow = kept.find(row => rowCategory(row) === 'Open');
    for (const row of rows(editor)) {
        row.querySelector('.status-color-button').innerHTML = statusDot({ category: rowCategory(row), color: row.dataset.color });
        row.querySelector('.status-default-badge').hidden = row !== defaultRow;
        if (!isRemoved(row)) continue;
        // Tasks can only move to a status that exists already.
        const targets = kept.filter(other => other.dataset.statusId);
        const select = row.querySelector('.status-replacement');
        const previous = select.value;
        const fallback = targets.find(other => rowCategory(other) === rowCategory(row)) || targets[0];
        select.innerHTML = Object.entries(statusCategories).map(([category, label]) => {
            const options = targets.filter(other => rowCategory(other) === category);
            return options.length ? `<optgroup label="${label}">${options.map(other => `<option value="${other.dataset.statusId}">${escapeHtml(rowName(other) || 'Ohne Namen')}</option>`).join('')}</optgroup>` : '';
        }).join('');
        select.value = targets.some(other => other.dataset.statusId === previous) ? previous : fallback?.dataset.statusId || '';
    }
}

function showError(editor, message = '') { editor.querySelector('.option-error').textContent = message; }

function nameTaken(editor, name, except = null) {
    const lower = name.toLocaleLowerCase('de');
    return rows(editor).some(row => row !== except && !isRemoved(row) && rowName(row).toLocaleLowerCase('de') === lower);
}

function addStatus(editor, section) {
    const input = section.querySelector('.status-new');
    const name = input.value.trim();
    showError(editor);
    if (!name) return;
    if (nameTaken(editor, name)) { showError(editor, `„${name}“ ist bereits vorhanden.`); return; }
    const used = new Set(rows(editor).map(row => row.dataset.color));
    const color = Object.keys(statusColors).find(item => !used.has(item)) || 'gray';
    section.querySelector('.status-list').insertAdjacentHTML('beforeend', statusRow({ id: null, name, category: section.dataset.category, color }, new Map()));
    input.value = '';
    input.focus();
    refresh(editor);
}

function removeStatus(editor, row) {
    showError(editor);
    const category = rowCategory(row);
    if (!rows(editor).some(other => other !== row && !isRemoved(other) && rowCategory(other) === category)) {
        showError(editor, `„${statusCategories[category]}“ braucht mindestens einen Status.`);
        return;
    }
    if (Number(row.dataset.inUse) > 0) {
        row.classList.add('removed');
        row.querySelector('.status-removed').hidden = false;
        row.querySelector('.status-swatches').hidden = true;
        row.querySelectorAll('.status-row-main input, .status-row-main button').forEach(control => control.disabled = true);
    } else row.remove();
    refresh(editor);
}

function restoreStatus(editor, row) {
    row.classList.remove('removed');
    row.querySelector('.status-removed').hidden = true;
    row.querySelectorAll('.status-row-main input, .status-row-main button').forEach(control => control.disabled = false);
    refresh(editor);
}

// Moves a row one place up or down; at the edge of a category it continues in the neighbouring one.
function moveRow(editor, row, direction) {
    const sibling = direction < 0 ? row.previousElementSibling : row.nextElementSibling;
    if (sibling) direction < 0 ? sibling.before(row) : sibling.after(row);
    else {
        const lists = [...editor.querySelectorAll('.status-list')];
        const next = lists[lists.indexOf(row.parentElement) + direction];
        if (!next) return;
        direction < 0 ? next.append(row) : next.prepend(row);
    }
    refresh(editor);
    row.querySelector('.status-drag-handle').focus();
}

// The statuses to save in display order, and where the tasks of removed statuses go.
// Text left in a "new status" input is included so it isn't lost.
export function readStatusEditor(editor) {
    const statuses = [];
    for (const section of editor.querySelectorAll('.status-category')) {
        for (const row of section.querySelectorAll('.status-row:not(.removed)'))
            statuses.push({ id: row.dataset.statusId || null, name: rowName(row), category: section.dataset.category, color: row.dataset.color });
        const pending = section.querySelector('.status-new').value.trim();
        if (pending) statuses.push({ id: null, name: pending, category: section.dataset.category, color: 'gray' });
    }
    const replacements = Object.fromEntries(rows(editor).filter(row => isRemoved(row) && row.dataset.statusId)
        .map(row => [row.dataset.statusId, row.querySelector('.status-replacement').value]).filter(([, target]) => target));
    return { statuses, replacements };
}

export function initializeStatusEditors(root) {
    let dragged = null;
    root.addEventListener('click', event => {
        const editor = event.target.closest('.status-editor');
        if (!editor) return;
        const row = event.target.closest('.status-row');
        if (event.target.closest('[data-status-add]')) addStatus(editor, event.target.closest('.status-category'));
        else if (event.target.closest('[data-status-remove]')) removeStatus(editor, row);
        else if (event.target.closest('[data-status-restore]')) restoreStatus(editor, row);
        else if (event.target.closest('[data-status-color-toggle]')) {
            const panel = row.querySelector('.status-swatches');
            panel.hidden = !panel.hidden;
            event.target.closest('[data-status-color-toggle]').setAttribute('aria-expanded', String(!panel.hidden));
        } else if (event.target.closest('[data-status-color]')) {
            const color = event.target.closest('[data-status-color]').dataset.statusColor;
            row.dataset.color = color;
            row.querySelectorAll('[data-status-color]').forEach(swatch => swatch.setAttribute('aria-pressed', String(swatch.dataset.statusColor === color)));
            refresh(editor);
        }
    });
    root.addEventListener('input', event => {
        const editor = event.target.closest('.status-editor');
        if (editor && event.target.matches('.status-name')) refresh(editor);
    });
    root.addEventListener('keydown', event => {
        const editor = event.target.closest('.status-editor');
        if (!editor) return;
        if (event.key === 'Enter' && event.target.matches('input')) {
            // Enter adds a status instead of submitting anything around the editor.
            event.preventDefault();
            if (event.target.matches('.status-new')) addStatus(editor, event.target.closest('.status-category'));
        }
        if (event.target.matches('.status-drag-handle') && ['ArrowUp', 'ArrowDown'].includes(event.key)) {
            event.preventDefault();
            moveRow(editor, event.target.closest('.status-row'), event.key === 'ArrowUp' ? -1 : 1);
        }
    });
    root.addEventListener('dragstart', event => {
        const handle = event.target.closest?.('.status-drag-handle');
        if (!handle) return;
        dragged = handle.closest('.status-row');
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', rowName(dragged));
        event.dataTransfer.setDragImage(dragged, 16, 16);
        dragged.classList.add('dragging');
    });
    root.addEventListener('dragover', event => {
        const list = event.target.closest?.('.status-category')?.querySelector('.status-list');
        if (!dragged || !list || !root.contains(dragged)) return;
        event.preventDefault();
        // The row moves while dragging, so the drop position is always visible.
        const after = [...list.querySelectorAll('.status-row')].find(row => row !== dragged &&
            event.clientY < row.getBoundingClientRect().top + row.offsetHeight / 2);
        if (after) { if (after.previousElementSibling !== dragged) after.before(dragged); }
        else if (list.lastElementChild !== dragged) list.append(dragged);
    });
    root.addEventListener('drop', event => { if (dragged) event.preventDefault(); });
    root.addEventListener('dragend', () => {
        if (!dragged) return;
        dragged.classList.remove('dragging');
        const editor = dragged.closest('.status-editor');
        dragged = null;
        if (editor) refresh(editor);
    });
}

// Called once the editor is in the page.
export const prepareStatusEditor = editor => refresh(editor);
