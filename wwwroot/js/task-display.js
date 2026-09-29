const storageKey = 'arbor-task-display';
const icons = {
    popup: '<rect x="3" y="4" width="18" height="16" rx="2"/><rect x="7" y="8" width="10" height="8" rx="1"/>',
    panel: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M14 4v16"/>'
};

export function initializeTaskDisplay(container, toggle) {
    let mode = 'panel';
    try { if (localStorage.getItem(storageKey) === 'popup') mode = 'popup'; }
    catch { /* Keep the default when browser storage is unavailable. */ }

    function applyMode() {
        container.classList.toggle('task-popup', mode === 'popup');
        const nextMode = mode === 'popup' ? 'panel' : 'popup';
        const label = nextMode === 'popup' ? 'Als Popup anzeigen' : 'Als Seitenpanel anzeigen';
        toggle.title = label;
        toggle.setAttribute('aria-label', label);
        toggle.innerHTML = `<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true">${icons[nextMode]}</svg>`;
    }

    toggle.addEventListener('click', () => {
        mode = mode === 'popup' ? 'panel' : 'popup';
        applyMode();
        try { localStorage.setItem(storageKey, mode); }
        catch { /* Switching still works when browser storage is unavailable. */ }
    });
    applyMode();
}
