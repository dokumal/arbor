const dayMs = 86400000;
const dayNumber = date => Date.parse(`${date}T00:00:00Z`) / dayMs;
const dateString = day => new Date(day * dayMs).toISOString().slice(0, 10);
const minDay = dayNumber('0001-01-01');
const maxDay = dayNumber('9999-12-31');

export function changeTimelineDates(task, mode, delta) {
    if (!task.startDate && !task.dueDate) return null;
    const start = dayNumber(task.startDate || task.dueDate);
    const end = dayNumber(task.dueDate || task.startDate);
    delta = Math.round(delta);
    if (mode === 'start') return {
        startDate: dateString(Math.max(minDay, Math.min(end, start + delta))), dueDate: dateString(end)
    };
    if (mode === 'end') return {
        startDate: dateString(start), dueDate: dateString(Math.min(maxDay, Math.max(start, end + delta)))
    };
    delta = Math.max(minDay - start, Math.min(maxDay - end, delta));
    return {
        startDate: task.startDate ? dateString(start + delta) : null,
        dueDate: task.dueDate ? dateString(end + delta) : null
    };
}

function dateLabel(value) {
    return new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' })
        .format(new Date(`${value}T00:00:00Z`));
}

export function initializeTimelineDrag(content, getTask, saveDates, onError) {
    let drag = null;
    let busy = false;
    let suppressClick = false;
    const preview = document.createElement('div');
    preview.className = 'timeline-date-preview';
    preview.hidden = true;
    document.body.append(preview);

    function restore(state) {
        if (!state.bar.isConnected) return;
        state.bar.style.cssText = state.originalStyle;
        state.bar.title = state.originalTitle;
        state.bar.classList.remove('timeline-editing', 'timeline-saving');
        state.bar.removeAttribute('aria-busy');
    }
    function release(state) {
        preview.hidden = true;
        state.bar.classList.remove('timeline-editing');
        if (state.bar.hasPointerCapture(state.pointerId)) state.bar.releasePointerCapture(state.pointerId);
    }
    function cancel() {
        if (!drag) return;
        const state = drag; drag = null;
        restore(state); release(state);
    }
    function paint(state, dates, x, y) {
        const start = dayNumber(dates.startDate || dates.dueDate);
        const end = dayNumber(dates.dueDate || dates.startDate);
        const left = Math.max(0, start - state.rangeStart);
        const right = Math.min(state.days, end - state.rangeStart + 1);
        state.bar.style.left = `${left / state.days * 100}%`;
        state.bar.style.width = `${Math.max((right - left) / state.days * 100, 1.5)}%`;
        state.bar.style.visibility = right > left ? '' : 'hidden';
        const label = `${dateLabel(dates.startDate || dates.dueDate)} – ${dateLabel(dates.dueDate || dates.startDate)}`;
        state.bar.title = `${state.task.title} · ${label}`;
        preview.textContent = `${label} · ${end - start + 1} ${end === start ? 'Tag' : 'Tage'}`;
        preview.style.left = `${Math.max(8, Math.min(x + 12, window.innerWidth - 325))}px`;
        preview.style.top = `${Math.max(8, Math.min(y + 20, window.innerHeight - 45))}px`;
        preview.hidden = false;
    }

    content.addEventListener('click', event => {
        if (suppressClick || event.target.closest('.timeline-resize')) {
            event.preventDefault(); event.stopPropagation();
        }
    }, true);
    content.addEventListener('pointerdown', event => {
        if (busy || drag || event.button !== 0 || event.isPrimary === false) return;
        const bar = event.target.closest('.timeline-bar');
        if (!bar) return;
        const task = getTask(bar.dataset.id);
        if (!task) return;
        const wrap = bar.closest('.timeline-wrap');
        const days = Number(wrap.dataset.days);
        drag = {
            bar, task, mode: event.target.closest('.timeline-resize')?.dataset.edge || 'move',
            pointerId: event.pointerId, x: event.clientX, changed: false,
            pixelsPerDay: bar.closest('.timeline-track').getBoundingClientRect().width / days,
            rangeStart: dayNumber(wrap.dataset.start), days,
            originalStyle: bar.style.cssText, originalTitle: bar.title,
            dates: { startDate: task.startDate, dueDate: task.dueDate }
        };
    });
    document.addEventListener('pointermove', event => {
        if (!drag || event.pointerId !== drag.pointerId) return;
        const offset = event.clientX - drag.x;
        if (!drag.changed && Math.abs(offset) < 4) return;
        if (!drag.changed) {
            drag.changed = true;
            drag.bar.setPointerCapture(event.pointerId);
            drag.bar.classList.add('timeline-editing');
        }
        event.preventDefault();
        drag.dates = changeTimelineDates(drag.task, drag.mode, offset / drag.pixelsPerDay);
        paint(drag, drag.dates, event.clientX, event.clientY);
    }, { passive: false });
    document.addEventListener('pointerup', async event => {
        if (!drag || event.pointerId !== drag.pointerId) return;
        const state = drag; drag = null;
        if (state.changed || state.mode !== 'move') {
            suppressClick = true;
            setTimeout(() => { suppressClick = false; }, 0);
        }
        release(state);
        if (!state.changed || (state.dates.startDate === state.task.startDate && state.dates.dueDate === state.task.dueDate)) {
            restore(state); return;
        }
        busy = true;
        state.bar.classList.add('timeline-saving');
        state.bar.setAttribute('aria-busy', 'true');
        try { await saveDates(state.task.id, state.dates); }
        catch (error) { restore(state); onError(error); }
        finally { busy = false; state.bar.classList.remove('timeline-saving'); state.bar.removeAttribute('aria-busy'); }
    });
    document.addEventListener('pointercancel', event => { if (drag?.pointerId === event.pointerId) cancel(); });
    content.addEventListener('lostpointercapture', event => { if (drag?.pointerId === event.pointerId) cancel(); });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && drag) { event.preventDefault(); cancel(); }
    });
    window.addEventListener('blur', cancel);
    // The same handles are usable without a pointer: arrows adjust by one day.
    content.addEventListener('keydown', async event => {
        const handle = event.target.closest('.timeline-resize');
        if (!handle || !['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
        event.preventDefault();
        if (busy || drag) return;
        const bar = handle.closest('.timeline-bar');
        const task = getTask(bar.dataset.id);
        if (!task) return;
        const dates = changeTimelineDates(task, handle.dataset.edge, (event.key === 'ArrowLeft' ? -1 : 1) * (event.shiftKey ? 7 : 1));
        if (dates.startDate === task.startDate && dates.dueDate === task.dueDate) return;
        busy = true;
        try {
            await saveDates(task.id, dates);
            const nextBar = [...content.querySelectorAll('.timeline-bar')].find(item => item.dataset.id === task.id);
            nextBar?.querySelector(`.timeline-resize.${handle.dataset.edge}`)?.focus();
        } catch (error) { onError(error); }
        finally { busy = false; }
    });
}
