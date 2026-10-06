// Editable list of select options. Existing rows remember their original value so a rename can be told apart from a new option.
import { escapeHtml } from './render.js?v=view-sorting';

const optionRow = (value, original = null) => `<div class="option-row"${original === null ? '' : ` data-original="${escapeHtml(original)}"`}><span class="option-bullet" aria-hidden="true"></span><input class="option-input" value="${escapeHtml(value)}" maxlength="500" aria-label="Option" /><button type="button" class="option-remove" data-option-remove title="Option entfernen" aria-label="Option „${escapeHtml(value)}“ entfernen">×</button></div>`;

export function renderOptionEditor(options = [], existing = false) {
    return `<div class="option-editor"><div class="option-list">${options.map(option => optionRow(option, existing ? option : null)).join('')}</div><div class="option-add"><input class="option-new" maxlength="500" placeholder="Neue Option …" aria-label="Neue Option" /><button type="button" class="secondary-button" data-option-add>＋ Hinzufügen</button></div><p class="option-error form-error" aria-live="polite"></p></div>`;
}

// Emptied rows count as removed; text left in the "new option" input is included so it isn't lost.
export function readOptions(editor) {
    const options = Array.from(editor.querySelectorAll('.option-row')).map(row =>
        ({ original: row.dataset.original ?? null, value: row.querySelector('.option-input').value.trim() }));
    const pending = editor.querySelector('.option-new').value.trim();
    if (pending) options.push({ original: null, value: pending });
    return options.filter(option => option.value);
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
