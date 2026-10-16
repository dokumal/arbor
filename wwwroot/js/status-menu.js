// Small popovers for statuses: edit the status of a Kanban column, add a status column and pick a task's status.
// Changes save the complete status list (see statuses.js), so they are queued one after another.
import { api } from './api.js?v=problem-details';
import { escapeHtml, statusDot } from './render.js?v=statuses';
import { allStatuses, saveStatusList, statusById, statusCategories, statusColors, unusedColor, unusedName } from './statuses.js';

export function initializeStatusMenu({ items, reload, notify, manageStatuses }) {
    const popover = document.createElement('div');
    popover.className = 'status-popover';
    popover.setAttribute('role', 'dialog');
    popover.hidden = true;
    document.body.append(popover);
    // { type: 'column' | 'delete' | 'add' | 'picker', statusId?, taskId?, group? }
    let menu = null;
    let anchor = null;
    let busy = false;

    function place() {
        if (!anchor?.isConnected) return;
        const rect = anchor.getBoundingClientRect();
        const width = popover.offsetWidth, height = popover.offsetHeight;
        const left = Math.min(Math.max(8, rect.left), window.innerWidth - width - 8);
        const above = rect.top - height - 6;
        const top = rect.bottom + 6 + height > window.innerHeight - 8 && above > 8 ? above : rect.bottom + 6;
        popover.style.left = `${left}px`;
        popover.style.top = `${Math.max(8, top)}px`;
    }

    function show(nextMenu, target, error = '') {
        menu = nextMenu;
        anchor = target;
        const content = { column: columnMenu, delete: deleteMenu, add: addMenu, picker: pickerMenu }[menu.type](error);
        if (content === null) return close();
        popover.innerHTML = content;
        popover.setAttribute('aria-label', { column: 'Status bearbeiten', delete: 'Status löschen', add: 'Status hinzufügen', picker: 'Status wählen' }[menu.type]);
        popover.hidden = false;
        anchor.setAttribute('aria-expanded', 'true');
        place();
        const focus = popover.querySelector('[data-autofocus]') || popover.querySelector('button:not(:disabled), input, select');
        focus?.focus();
        if (focus?.matches('input')) focus.select();
    }

    function close({ restoreFocus = true } = {}) {
        if (popover.hidden) return;
        popover.hidden = true;
        popover.innerHTML = '';
        anchor?.setAttribute('aria-expanded', 'false');
        if (restoreFocus && anchor?.isConnected) anchor.focus();
        menu = null;
        anchor = null;
    }

    // Views are re-rendered after saving, so the heading of the status is looked up again.
    function headingFor(statusId, group) {
        const headings = [...document.querySelectorAll(`[data-action="status-menu"][data-status-id="${statusId}"]`)];
        return headings.find(heading => heading.closest('.kanban-column')?.dataset.group === group) || headings[0] || null;
    }

    async function run(task, onError) {
        if (busy) return;
        busy = true;
        try { await task(); }
        catch (error) { onError(error); }
        finally { busy = false; }
    }

    // The typed name counts as well, so a colour change doesn't drop it.
    function draft(status, changes = {}) {
        const name = popover.querySelector('#status-menu-name')?.value.trim();
        return { ...status, name: name || status.name, ...changes };
    }

    async function saveAndReopen(list, replacements, reopenId) {
        const group = menu.group;
        await saveStatusList(list, replacements);
        await reload();
        const target = reopenId && headingFor(reopenId, group);
        if (target) show({ type: 'column', statusId: reopenId, group }, target);
        else close({ restoreFocus: false });
    }

    function updateStatus(changes, { keepOpen = true } = {}) {
        const status = statusById(menu.statusId);
        if (!status) return close();
        const list = allStatuses().map(item => item.id === status.id ? draft(item, changes) : item);
        const current = menu;
        run(async () => {
            await saveAndReopen(list, {}, keepOpen ? status.id : null);
            notify('Status gespeichert.');
        }, error => show(current, anchor, error.message));
    }

    function columnMenu(error) {
        const status = statusById(menu.statusId);
        if (!status) return null;
        const swatches = Object.entries(statusColors).map(([color, label]) =>
            `<button type="button" class="status-swatch status-color-${color}" data-menu="color" data-color="${color}" aria-pressed="${color === status.color}" title="${label}" aria-label="Farbe ${label}"></button>`).join('');
        const categories = Object.entries(statusCategories).map(([category, label]) =>
            `<button type="button" class="status-category-choice" data-menu="category" data-category="${category}" aria-pressed="${category === status.category}">${statusDot({ category, color: status.color })}${label}</button>`).join('');
        return `<form data-menu-form="rename"><label class="status-menu-label" for="status-menu-name">Name</label><input id="status-menu-name" class="status-menu-input" maxlength="50" value="${escapeHtml(status.name)}" autocomplete="off" data-autofocus /></form>
            <div class="status-menu-label">Farbe</div><div class="status-swatches" role="group" aria-label="Farbe">${swatches}</div>
            <div class="status-menu-label">Kategorie</div><div class="status-category-choices" role="group" aria-label="Kategorie">${categories}</div>
            <div class="status-menu-separator"></div>
            <button type="button" class="status-menu-item" data-menu="insert" data-offset="0">← Status links einfügen</button>
            <button type="button" class="status-menu-item" data-menu="insert" data-offset="1">→ Status rechts einfügen</button>
            <button type="button" class="status-menu-item" data-menu="manage">Alle Status verwalten …</button>
            <div class="status-menu-separator"></div>
            <button type="button" class="status-menu-item danger" data-menu="delete">Löschen …</button>
            <p class="form-error status-menu-error" role="alert">${escapeHtml(error)}</p>`;
    }

    function deleteMenu(error) {
        const status = statusById(menu.statusId);
        if (!status) return null;
        const count = items().filter(item => item.statusId === status.id).length;
        const others = allStatuses().filter(item => item.id !== status.id);
        const last = !others.some(item => item.category === status.category);
        const fallback = others.find(item => item.category === status.category);
        const target = last ? `<p class="status-menu-text">„${escapeHtml(status.name)}“ ist der letzte Status in „${statusCategories[status.category]}“. Jede Kategorie braucht mindestens einen Status.</p>`
            : count ? `<label class="status-menu-label" for="status-menu-replacement">${count} ${count === 1 ? 'Aufgabe' : 'Aufgaben'} verschieben nach</label><select id="status-menu-replacement" class="status-menu-input">${statusOptions(others, fallback?.id)}</select>`
            : '<p class="status-menu-text">Keine Aufgabe hat diesen Status.</p>';
        return `<p class="status-menu-title">„${escapeHtml(status.name)}“ löschen?</p>${target}
            <div class="status-menu-actions"><button type="button" class="secondary-button" data-menu="back">Abbrechen</button>${last ? '' : '<button type="button" class="danger-solid-button" data-menu="confirm-delete">Löschen</button>'}</div>
            <p class="form-error status-menu-error" role="alert">${escapeHtml(error)}</p>`;
    }

    function addMenu(error) {
        const categories = Object.entries(statusCategories).map(([category, label]) =>
            `<option value="${category}" ${category === (menu.category || 'Active') ? 'selected' : ''}>${label}</option>`).join('');
        return `<form data-menu-form="add"><label class="status-menu-label" for="status-menu-new">Name</label><input id="status-menu-new" class="status-menu-input" maxlength="50" required placeholder="Zum Beispiel: Wartet auf" autocomplete="off" value="${escapeHtml(menu.name || '')}" data-autofocus />
            <label class="status-menu-label" for="status-menu-category">Kategorie</label><select id="status-menu-category" class="status-menu-input">${categories}</select>
            <div class="status-menu-actions"><button type="button" class="secondary-button" data-menu="cancel">Abbrechen</button><button type="submit" class="primary-button">Hinzufügen</button></div>
            <p class="form-error status-menu-error" role="alert">${escapeHtml(error)}</p></form>`;
    }

    function pickerMenu(error) {
        const task = items().find(item => item.id === menu.taskId);
        if (!task) return null;
        return Object.entries(statusCategories).map(([category, label]) => {
            const statuses = allStatuses().filter(status => status.category === category);
            return `<div class="status-menu-label">${label}</div>${statuses.map(status => `<button type="button" class="status-menu-item" data-menu="pick" data-status-id="${status.id}" aria-pressed="${status.id === task.statusId}">${statusDot(status)}<span>${escapeHtml(status.name)}</span>${status.id === task.statusId ? '<span class="status-menu-check" aria-hidden="true">✓</span>' : ''}</button>`).join('')}`;
        }).join('') + `<p class="form-error status-menu-error" role="alert">${escapeHtml(error)}</p>`;
    }

    popover.addEventListener('submit', event => {
        event.preventDefault();
        const form = event.target.dataset.menuForm;
        if (form === 'rename') updateStatus({}, { keepOpen: false });
        if (form === 'add') {
            const name = popover.querySelector('#status-menu-new').value.trim();
            const category = popover.querySelector('#status-menu-category').value;
            const current = { ...menu, name, category };
            if (!name) return;
            // Appended to the list, the status becomes the last one of its category.
            run(async () => {
                await saveStatusList([...allStatuses(), { id: null, name, category, color: unusedColor() }]);
                await reload();
                close({ restoreFocus: false });
                notify(`Status „${name}“ hinzugefügt.`);
            }, error => show(current, anchor, error.message));
        }
    });

    popover.addEventListener('click', event => {
        const action = event.target.closest('[data-menu]');
        if (!action || !menu) return;
        const status = statusById(menu.statusId);
        switch (action.dataset.menu) {
            case 'color': updateStatus({ color: action.dataset.color }); break;
            case 'category': if (action.dataset.category !== status?.category) updateStatus({ category: action.dataset.category }); break;
            case 'insert': {
                if (!status) return close();
                const list = allStatuses().map(item => item.id === status.id ? draft(item) : item);
                const name = unusedName();
                list.splice(list.findIndex(item => item.id === status.id) + Number(action.dataset.offset), 0,
                    { id: null, name, category: status.category, color: unusedColor() });
                const current = menu;
                run(async () => {
                    await saveStatusList(list);
                    await reload();
                    const created = allStatuses().find(item => item.name === name);
                    const target = created && headingFor(created.id, current.group);
                    if (target) show({ type: 'column', statusId: created.id, group: current.group }, target);
                    else close({ restoreFocus: false });
                }, error => show(current, anchor, error.message));
                break;
            }
            case 'manage': close({ restoreFocus: false }); manageStatuses(); break;
            case 'delete': show({ ...menu, type: 'delete' }, anchor); break;
            case 'back': show({ ...menu, type: 'column' }, anchor); break;
            case 'cancel': close(); break;
            case 'confirm-delete': {
                if (!status) return close();
                const replacement = popover.querySelector('#status-menu-replacement')?.value;
                const current = menu;
                run(async () => {
                    await saveStatusList(allStatuses().filter(item => item.id !== status.id), replacement ? { [status.id]: replacement } : {});
                    await reload();
                    close({ restoreFocus: false });
                    notify(`Status „${status.name}“ gelöscht.`);
                }, error => show(current, anchor, error.message));
                break;
            }
            case 'pick': {
                const current = menu;
                run(async () => {
                    await setTaskStatus(current.taskId, action.dataset.statusId);
                    close({ restoreFocus: false });
                }, error => show(current, anchor, error.message));
                break;
            }
        }
    });

    // A click outside saves a typed name; Escape discards it.
    document.addEventListener('mousedown', event => {
        if (popover.hidden || popover.contains(event.target) || anchor?.contains(event.target)) return;
        const input = popover.querySelector('#status-menu-name');
        const status = statusById(menu?.statusId);
        if (menu?.type === 'column' && input && status && input.value.trim() && input.value.trim() !== status.name) updateStatus({}, { keepOpen: false });
        else close({ restoreFocus: false });
    }, true);
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape' || popover.hidden) return;
        event.stopPropagation();
        close();
    }, true);
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);

    async function setTaskStatus(taskId, statusId) {
        const task = items().find(item => item.id === taskId);
        if (!task || task.statusId === statusId) return;
        await api(`/api/items/${taskId}/status`, { method: 'PATCH', body: JSON.stringify({ statusId }) });
        await reload();
        notify(`Status: ${statusById(statusId)?.name || 'geändert'}.`);
    }

    // Opening the popover of the same anchor again closes it.
    const toggle = (nextMenu, target) => anchor === target && !popover.hidden ? close() : show(nextMenu, target);
    return {
        openColumnMenu: target => toggle({ type: 'column', statusId: target.dataset.statusId, group: target.closest('.kanban-column')?.dataset.group }, target),
        openAddStatus: target => toggle({ type: 'add' }, target),
        openPicker: (target, taskId) => toggle({ type: 'picker', taskId }, target),
        setTaskStatus
    };
}

// Options grouped by category, for a status <select>.
export function statusOptions(statuses, selectedId) {
    return Object.entries(statusCategories).map(([category, label]) => {
        const options = statuses.filter(status => status.category === category);
        return options.length ? `<optgroup label="${label}">${options.map(status => `<option value="${status.id}" ${status.id === selectedId ? 'selected' : ''}>${escapeHtml(status.name)}</option>`).join('')}</optgroup>` : '';
    }).join('');
}
