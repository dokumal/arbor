// Editable list of select options. Existing rows keep their option id so a rename can be told apart from a new option.
import { escapeHtml } from './render.js?v=statuses';

const optionRow = (name, id = null) => `<div class="option-row"${id === null ? '' : ` data-option-id="${escapeHtml(id)}"`}><span class="option-bullet" aria-hidden="true"></span><input class="option-input" value="${escapeHtml(name)}" maxlength="500" aria-label="Option" /><button type="button" class="option-remove" data-option-remove title="Option entfernen" aria-label="Option „${escapeHtml(name)}“ entfernen">×</button></div>`;

// Takes option names for a new field, or { id, name } objects for an existing one.
export function renderOptionEditor(options = []) {
    return `<div class="option-editor"><div class="option-list">${options.map(option => typeof option === 'string' ? optionRow(option) : optionRow(option.name, option.id)).join('')}</div><div class="option-add"><input class="option-new" maxlength="500" placeholder="Neue Option …" aria-label="Neue Option" /><button type="button" class="secondary-button" data-option-add>＋ Hinzufügen</button></div><p class="option-error form-error" aria-live="polite"></p></div>`;
}

// Emptied rows count as removed; text left in the "new option" input is included so it isn't lost.
export function readOptions(editor) {
    const options = Array.from(editor.querySelectorAll('.option-row')).map(row =>
        ({ id: row.dataset.optionId ?? null, name: row.querySelector('.option-input').value.trim() }));
    const pending = editor.querySelector('.option-new').value.trim();
    if (pending) options.push({ id: null, name: pending });
    return options.filter(option => option.name);
}

function addOption(editor) {
    const input = editor.querySelector('.option-new');
    const value = input.value.trim();
    const error = editor.querySelector('.option-error');
    error.textContent = '';
    if (!value) return;
    if (Array.from(editor.querySelectorAll('.option-input')).some(option => option.value.trim() === value)) {
        error.textContent = `„${value}“ ist bereits vorhanden.`;
        return;
    }
    editor.querySelector('.option-list').insertAdjacentHTML('beforeend', optionRow(value));
    input.value = '';
    input.focus();
}

export function initializeOptionEditors(root) {
    root.addEventListener('click', event => {
        const editor = event.target.closest('.option-editor');
        if (!editor) return;
        if (event.target.closest('[data-option-add]')) addOption(editor);
        event.target.closest('[data-option-remove]')?.closest('.option-row').remove();
    });
    root.addEventListener('keydown', event => {
        const editor = event.target.closest('.option-editor');
        if (event.key !== 'Enter' || !editor) return;
        // Enter adds an option instead of submitting the surrounding form.
        event.preventDefault();
        if (event.target.matches('.option-new')) addOption(editor);
        else editor.querySelector('.option-new').focus();
    });
}
