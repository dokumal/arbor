import { Editor, Image, Placeholder, StarterKit, TableKit, TaskItem, TaskList } from '../lib/tiptap/tiptap.js';
import { api } from './api.js?v=problem-details';
import { escapeHtml } from './render.js?v=statuses';
import { SlashCommands } from './slash-commands.js';
import { FormatMenu } from './format-menu.js';
import { attachTableToolbar } from './table-toolbar.js';
import { collapsibleBlocks } from './collapsible-blocks.js';

const imageTypes = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];
const maxImageBytes = 5 * 1024 * 1024;
let editor;
const host = () => document.getElementById('task-description');
const errorLine = () => document.getElementById('task-description-error');

function editableHtml(value) {
    if (!value) return '';
    if (/<\/?(?:p|div|h[1-6]|ul|ol|li|table|tbody|tr|td|th|figure|img|details|summary|span|strong|em|b|i|br)\b/i.test(value))
        return value;
    return `<p>${escapeHtml(value).replace(/\r?\n/g, '<br>')}</p>`;
}

async function uploadImages(files, position) {
    errorLine().textContent = '';
    for (const file of files) {
        try {
            // The server enforces the same limit, but larger requests are cut off before they reach it.
            if (file.size > maxImageBytes) throw new Error(`„${file.name}“ ist größer als 5 MB.`);
            const body = new FormData();
            body.append('image', file);
            const { url } = await api('/api/task-images', { method: 'POST', body });
            if (!editor || editor.isDestroyed) return;
            const chain = editor.chain().focus();
            if (position != null) chain.setTextSelection(Math.min(position, editor.state.doc.content.size));
            chain.setImage({ src: url, alt: file.name }).run();
            position = null;
        } catch (error) { errorLine().textContent = error.message; }
    }
}

function pickImage() {
    const picker = document.createElement('input');
    picker.type = 'file';
    picker.accept = imageTypes.join(',');
    picker.multiple = true;
    picker.addEventListener('change', () => uploadImages([...picker.files]));
    picker.click();
}

const imageFiles = list => [...(list || [])].filter(file => imageTypes.includes(file.type));

// A fresh editor per opened entry, so undo never reaches back into the previously opened one.
export function setDescription(value) {
    editor?.destroy();
    errorLine().textContent = '';
    editor = new Editor({
        element: host(),
        content: editableHtml(value),
        extensions: [
            StarterKit.configure({ heading: { levels: [1, 2, 3] }, link: { openOnClick: false } }),
            Image,
            // Checklists; the views show their progress (see checklistProgress in render.js).
            TaskList,
            TaskItem.configure({ nested: true, a11y: { checkboxLabel: node => `Abhaken: ${node.textContent || 'leerer Punkt'}` } }),
            TableKit.configure({ table: { resizable: true } }),
            ...collapsibleBlocks,
            // includeChildren reaches the titles of collapsible blocks; the stylesheet shows only those and the empty editor's.
            Placeholder.configure({
                includeChildren: true,
                showOnlyCurrent: false,
                placeholder: ({ node }) => node.type.name === 'detailsSummary' ? 'Titel' : 'Details, Notizen und nächste Schritte … Tippe „/“ für Befehle.'
            }),
            SlashCommands(pickImage),
            FormatMenu()
        ],
        editorProps: {
            attributes: { class: 'rich-text', 'aria-labelledby': 'item-content-label', 'aria-multiline': 'true', role: 'textbox' },
            handlePaste(view, event) {
                const files = imageFiles(event.clipboardData?.files);
                if (!files.length) return false;
                uploadImages(files);
                return true;
            },
            handleDrop(view, event, slice, moved) {
                const files = moved ? [] : imageFiles(event.dataTransfer?.files);
                if (!files.length) return false;
                uploadImages(files, view.posAtCoords({ left: event.clientX, top: event.clientY })?.pos);
                return true;
            }
        }
    });
    attachTableToolbar(editor, host());
}

export function getDescription() {
    return !editor || editor.isEmpty ? '' : editor.getHTML();
}
