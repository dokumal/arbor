import { escapeHtml } from './render.js';

let editor;
const input = () => document.getElementById('task-description');

function editableHtml(value) {
    if (!value) return '';
    if (/<\/?(?:p|div|h[1-6]|ul|ol|li|table|tbody|tr|td|th|figure|img|span|strong|em|b|i|br)\b/i.test(value))
        return value;
    return `<p>${escapeHtml(value).replace(/\r?\n/g, '<br>')}</p>`;
}

export function setDescription(value) {
    const html = editableHtml(value);
    if (!editor && window.SUNEDITOR) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        editor = window.SUNEDITOR.create(input(), {
            plugins: {
                blockStyle: window.SUNEDITOR.plugins.blockStyle,
                list: window.SUNEDITOR.plugins.list,
                table: window.SUNEDITOR.plugins.table,
                image: window.SUNEDITOR.plugins.image
            },
            buttonList: [
                ['undo', 'redo'],
                ['bold', 'italic', 'underline'],
                ['blockStyle', 'list', 'table', 'image']
            ],
            value: html,
            statusbar: false,
            minHeight: '190px',
            placeholder: 'Details, Notizen und nächste Schritte …',
            image: {
                uploadUrl: '/api/task-images',
                uploadHeaders: token ? { RequestVerificationToken: token } : {},
                uploadSingleSizeLimit: 5 * 1024 * 1024,
                acceptedFormats: 'image/png,image/jpeg,image/gif,image/webp',
                createUrlInput: false
            }
        });
        return;
    }
    if (editor) editor.$.html.set(html);
    else input().value = value || '';
}

export function getDescription() {
    return editor ? editor.$.html.get() : input().value;
}
