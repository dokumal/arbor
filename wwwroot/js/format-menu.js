import { BubbleMenu, isTextSelection } from '../lib/tiptap/tiptap.js';
import { inCollapsibleTitle, setTextLevel } from './collapsible-blocks.js';

const inList = editor => ['bulletList', 'orderedList', 'taskList'].some(name => editor.isActive(name));

// The same formats the slash menu and the keyboard shortcuts offer, applied to the selected text. clearNodes lifts
// the selection out of a list first, and the list commands turn a heading into a list item on their own.
const groups = [
    [
        ['<b>B</b>', 'Fett (Strg+B)', chain => chain.toggleBold(), editor => editor.isActive('bold')],
        ['<i>I</i>', 'Kursiv (Strg+I)', chain => chain.toggleItalic(), editor => editor.isActive('italic')],
        ['<u>U</u>', 'Unterstrichen (Strg+U)', chain => chain.toggleUnderline(), editor => editor.isActive('underline')],
        ['<s>S</s>', 'Durchgestrichen (Strg+Umschalt+S)', chain => chain.toggleStrike(), editor => editor.isActive('strike')]
    ],
    [
        ['Text', 'Normaler Absatz', (chain, editor) => inCollapsibleTitle(editor) ? setTextLevel(chain, editor, null) : chain.clearNodes(),
            editor => editor.isActive('paragraph') && !inList(editor) || editor.isActive('detailsSummary', { level: null })],
        ...[1, 2, 3].map(level => [`H${level}`, `Überschrift ${level}`,
            (chain, editor) => inCollapsibleTitle(editor) ? setTextLevel(chain, editor, level) : chain.clearNodes().setHeading({ level }),
            editor => editor.isActive('heading', { level }) || editor.isActive('detailsSummary', { level })])
    ],
    [
        ['•&#8202;≡', 'Aufzählung', chain => chain.toggleBulletList(), editor => editor.isActive('bulletList')],
        ['1.&#8202;≡', 'Nummerierte Liste', chain => chain.toggleOrderedList(), editor => editor.isActive('orderedList')],
        ['☑', 'Checkliste', chain => chain.toggleTaskList(), editor => editor.isActive('taskList')]
    ]
];
const commands = groups.flat();

// Tiptap's bubble menu shows the bar above selected text and positions it; it is appended to the editor host,
// so it scrolls along with the content. Images and table cell selections keep their own handling.
export function FormatMenu() {
    const bar = document.createElement('div');
    bar.className = 'format-menu';
    bar.setAttribute('role', 'toolbar');
    bar.setAttribute('aria-label', 'Text formatieren');
    let index = 0;
    bar.innerHTML = groups.map(buttons => `<div class="format-menu-group">${buttons.map(([label, title]) =>
        `<button type="button" data-index="${index++}" title="${title}" aria-label="${title}" aria-pressed="false">${label}</button>`).join('')}</div>`).join('');
    // No preventDefault on mousedown here: the bubble menu tracks the focus moving to a button and back itself,
    // and keeping the focus in the editor would leave it waiting for a blur and stay open on the next one.

    let editor = null;
    const refresh = () => bar.querySelectorAll('[data-index]').forEach(button =>
        button.setAttribute('aria-pressed', String(commands[button.dataset.index][3](editor))));
    bar.addEventListener('click', event => {
        const button = event.target.closest('[data-index]');
        if (!button || !editor) return;
        // Focus back synchronously (focus() in a chain waits a frame), so the menu never sees the editor unfocused.
        editor.view.focus();
        commands[button.dataset.index][2](editor.chain(), editor).run();
    });

    return BubbleMenu.extend({
        onCreate() { editor = this.editor; },
        onTransaction() { if (bar.isConnected) refresh(); }
    }).configure({
        element: bar,
        pluginKey: 'formatMenu',
        updateDelay: 150,
        shouldShow: ({ editor, state, from, to }) => editor.isEditable && (editor.view.hasFocus() || bar.contains(document.activeElement))
            && isTextSelection(state.selection) && !state.selection.empty && state.doc.textBetween(from, to).trim() !== '',
        options: { placement: 'top', offset: 8, flip: { padding: 8 }, shift: { padding: 8 }, onShow: () => refresh() }
    });
}
