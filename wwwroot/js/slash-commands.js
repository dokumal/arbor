import { Extension, Suggestion } from '../lib/tiptap/tiptap.js';
import { escapeHtml } from './render.js?v=statuses';
import { inCollapsibleTitle, setCollapsible, setTextLevel } from './collapsible-blocks.js';

// Table commands are only offered (and listed first) while the cursor is inside a table, inserting a table only outside one.
// In the title of a collapsible block only text and heading levels are offered.
function commandList(pickImage) {
    const block = (title, hint, keywords, run) => ({ title, hint, keywords, run });
    const level = (title, hint, keywords, value) => ({ title, hint, keywords, inTitle: true, run: (chain, editor) => setTextLevel(chain, editor, value) });
    const collapsible = (title, hint, keywords, value) => block(title, hint, `aufklappen zuklappen toggle collapsible details ${keywords}`, chain => chain.command(setCollapsible(value)));
    const table = (title, keywords, command) => ({ title, hint: '', keywords, table: true, run: chain => chain[command]() });
    return [
        table('Zeile darüber einfügen', 'row zeile oben', 'addRowBefore'),
        table('Zeile darunter einfügen', 'row zeile unten', 'addRowAfter'),
        table('Spalte links einfügen', 'column spalte', 'addColumnBefore'),
        table('Spalte rechts einfügen', 'column spalte', 'addColumnAfter'),
        table('Kopfzeile ein/aus', 'header kopf', 'toggleHeaderRow'),
        table('Zeile löschen', 'row zeile entfernen', 'deleteRow'),
        table('Spalte löschen', 'column spalte entfernen', 'deleteColumn'),
        table('Tabelle löschen', 'table entfernen', 'deleteTable'),
        level('Text', 'Normaler Absatz', 'absatz paragraph normal', null),
        level('Überschrift 1', 'Große Überschrift', 'h1 heading titel', 1),
        level('Überschrift 2', 'Mittlere Überschrift', 'h2 heading', 2),
        level('Überschrift 3', 'Kleine Überschrift', 'h3 heading', 3),
        block('Aufzählung', 'Liste mit Punkten', 'liste bullet ul punkte', chain => chain.toggleBulletList()),
        block('Nummerierte Liste', 'Liste mit Zahlen', 'liste ordered ol nummern', chain => chain.toggleOrderedList()),
        block('Checkliste', 'Liste mit Kästchen zum Abhaken', 'checkbox todo aufgaben haken task', chain => chain.toggleTaskList()),
        collapsible('Aufklappbereich', 'Titel mit auf- und zuklappbarem Inhalt', '', null),
        collapsible('Aufklappbare Überschrift 1', 'Große Überschrift zum Aufklappen', 'h1 heading', 1),
        collapsible('Aufklappbare Überschrift 2', 'Mittlere Überschrift zum Aufklappen', 'h2 heading', 2),
        collapsible('Aufklappbare Überschrift 3', 'Kleine Überschrift zum Aufklappen', 'h3 heading', 3),
        { ...block('Tabelle', '3 × 3 mit Kopfzeile', 'table raster', chain => chain.insertTable({ rows: 3, cols: 3, withHeaderRow: true })), outsideTable: true },
        block('Bild', 'Datei hochladen', 'image foto grafik upload', chain => { chain.run(); pickImage(); return null; })
    ];
}

function matches(item, query) {
    const text = `${item.title} ${item.keywords}`.toLocaleLowerCase('de');
    return query.toLocaleLowerCase('de').split(/\s+/).every(part => text.includes(part));
}

// A small vanilla popup for the suggestion plugin. It lives in <body>, because the drawer is transformed in popup mode
// and would otherwise become the containing block of a fixed-position element.
function slashMenu() {
    let element = null, props = null, selected = 0;
    const reposition = () => {
        const rect = props?.clientRect?.();
        if (!element || !rect) return;
        const height = element.offsetHeight;
        const below = rect.bottom + 6 + height <= window.innerHeight;
        element.style.left = `${Math.max(8, Math.min(rect.left, window.innerWidth - element.offsetWidth - 8))}px`;
        element.style.top = `${below ? rect.bottom + 6 : Math.max(8, rect.top - height - 6)}px`;
    };
    const choose = index => { const item = props.items[index]; if (item) props.command(item); };
    const draw = () => {
        if (!props.items.length) {
            element.innerHTML = '<div class="slash-empty">Kein passender Befehl</div>';
        } else {
            let lastGroup = null;
            element.innerHTML = props.items.map((item, index) => {
                const group = item.table ? 'TABELLE' : 'EINFÜGEN';
                const heading = group !== lastGroup ? `<div class="slash-group">${group}</div>` : '';
                lastGroup = group;
                return `${heading}<button type="button" class="slash-item${index === selected ? ' selected' : ''}" data-index="${index}" role="option" aria-selected="${index === selected}"><span>${escapeHtml(item.title)}</span>${item.hint ? `<small>${escapeHtml(item.hint)}</small>` : ''}</button>`;
            }).join('');
        }
        element.querySelector('.selected')?.scrollIntoView({ block: 'nearest' });
        reposition();
    };
    const close = () => {
        window.removeEventListener('scroll', reposition, true);
        window.removeEventListener('resize', reposition);
        element?.remove();
        element = null;
    };
    return {
        onStart(next) {
            props = next;
            selected = 0;
            element = document.createElement('div');
            element.className = 'slash-menu';
            element.setAttribute('role', 'listbox');
            // Keeps the focus (and with it the suggestion) in the editor while clicking an entry.
            element.addEventListener('mousedown', event => event.preventDefault());
            element.addEventListener('click', event => {
                const button = event.target.closest('[data-index]');
                if (button) choose(Number(button.dataset.index));
            });
            document.body.append(element);
            window.addEventListener('scroll', reposition, true);
            window.addEventListener('resize', reposition);
            draw();
        },
        onUpdate(next) {
            props = next;
            selected = Math.min(selected, Math.max(0, props.items.length - 1));
            draw();
        },
        onKeyDown({ event }) {
            if (event.key === 'Escape') { close(); return true; }
            const count = props.items.length;
            if (!count) return false;
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                selected = (selected + (event.key === 'ArrowDown' ? 1 : -1) + count) % count;
                draw();
                return true;
            }
            if (event.key === 'Enter' || event.key === 'Tab') { choose(selected); return true; }
            return false;
        },
        onExit: close
    };
}

export function SlashCommands(pickImage) {
    const commands = commandList(pickImage);
    return Extension.create({
        name: 'slashCommands',
        addProseMirrorPlugins() {
            return [Suggestion({
                editor: this.editor,
                char: '/',
                items: ({ editor, query }) => {
                    const inTable = editor.isActive('table');
                    const inTitle = inCollapsibleTitle(editor);
                    return commands.filter(item => (inTitle ? item.inTitle : item.table ? inTable : !(item.outsideTable && inTable)) && matches(item, query));
                },
                command: ({ editor, range, props: item }) => {
                    const chain = editor.chain().focus().deleteRange(range);
                    item.run(chain, editor)?.run();
                },
                render: slashMenu
            })];
        }
    });
}
