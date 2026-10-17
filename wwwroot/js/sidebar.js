import { api } from './api.js?v=problem-details';
import { projectId } from './project.js';

// The sidebar is closed, open above the page, or pinned beside it. Only "pinned" is remembered across pages;
// the inline script in the layout applies it before the first paint. Narrow screens always show it above the page.
const $ = id => document.getElementById(id);
const root = document.documentElement;
const storageKey = 'arbor-sidebar';
const wide = window.matchMedia('(min-width: 900px)');
const isPinned = () => root.classList.contains('sidebar-pinned') && wide.matches;
const isOpen = () => root.classList.contains('sidebar-open');

function remember(pinned) {
    try { pinned ? localStorage.setItem(storageKey, 'pinned') : localStorage.removeItem(storageKey); } catch { /* Not remembered then. */ }
}

function update() {
    const visible = isOpen() || isPinned();
    $('sidebar-toggle').setAttribute('aria-expanded', String(visible));
    $('sidebar-pin').setAttribute('aria-pressed', String(isPinned()));
    const label = isPinned() ? 'Fixierung lösen' : 'Seitenleiste fixieren';
    $('sidebar-pin').title = label;
    $('sidebar-pin').setAttribute('aria-label', label);
    // Hidden links stay out of the tab order.
    $('sidebar').inert = !visible;
}

function setOpen(open) {
    root.classList.toggle('sidebar-open', open);
    update();
    if (open) $('sidebar').querySelector('[aria-current=page], a')?.focus({ preventScroll: true });
}

function setPinned(pinned) {
    root.classList.toggle('sidebar-pinned', pinned);
    root.classList.remove('sidebar-open');
    remember(pinned);
    update();
}

// The menu button closes a pinned sidebar as well; it then stays closed on the next pages.
$('sidebar-toggle').addEventListener('click', () => {
    if (isPinned()) setPinned(false);
    else setOpen(!isOpen());
});
$('sidebar-pin').addEventListener('click', () => setPinned(!isPinned()));
$('sidebar-backdrop').addEventListener('click', () => setOpen(false));
document.addEventListener('keydown', event => {
    if (event.key !== 'Escape' || !isOpen() || isPinned() || document.querySelector('dialog[open]')) return;
    // Escape closes the sidebar first, before other open panels.
    event.stopImmediatePropagation();
    setOpen(false);
    $('sidebar-toggle').focus();
}, true);
wide.addEventListener('change', update);
// Without a stored setting (blocked storage) the class from the layout script is missing, which simply means "not pinned".
update();

// Projects
let editingProject = null;

function openProjectDialog(project = null) {
    editingProject = project;
    $('project-dialog-heading').textContent = project ? 'Projekt bearbeiten' : 'Neues Projekt';
    $('project-name').value = project?.name ?? '';
    $('project-new-hint').hidden = !!project;
    $('delete-project-button').hidden = !project;
    $('project-form-error').textContent = '';
    if (!isPinned()) setOpen(false);
    $('project-dialog').showModal();
    $('project-name').select();
}


$('add-project-button').addEventListener('click', () => openProjectDialog());
document.querySelectorAll('.project-edit').forEach(button => button.addEventListener('click', () =>
    openProjectDialog({ id: button.dataset.id, name: button.dataset.name })));
document.querySelectorAll('#project-dialog .dialog-close').forEach(button => button.addEventListener('click', () => $('project-dialog').close()));

$('project-form').addEventListener('submit', async event => {
    event.preventDefault();
    $('project-form-error').textContent = '';
    const body = JSON.stringify({ name: $('project-name').value.trim() });
    try {
        if (editingProject) {
            await api(`/api/projects/${editingProject.id}`, { method: 'PUT', body });
            // Names appear in the sidebar, the header and the page title, which the server renders.
            location.reload();
        } else {
            const project = await api('/api/projects', { method: 'POST', body });
            location.href = `/projekte/${project.id}`;
        }
    } catch (error) { $('project-form-error').textContent = error.message; }
});

$('delete-project-button').addEventListener('click', async () => {
    if (!editingProject || !confirm(`Projekt „${editingProject.name}“ mit allen Aufgaben, Dokumenten, Ordnern, Status, Feldern und Ansichten endgültig löschen?`)) return;
    try {
        await api(`/api/projects/${editingProject.id}`, { method: 'DELETE' });
        if (editingProject.id === projectId) location.href = '/';
        else location.reload();
    } catch (error) { $('project-form-error').textContent = error.message; }
});
