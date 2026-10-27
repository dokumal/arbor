import { Details, DetailsContent, DetailsSummary, TextSelection } from '../lib/tiptap/tiptap.js';

const levels = [1, 2, 3];

// The title line of a collapsible block holds only text, so its heading level is an attribute on it (data-level).
const Summary = DetailsSummary.extend({
    // Ahead of the Enter handling of Details, which would add another line above the empty one of a new block.
    priority: 101,
    addKeyboardShortcuts() {
        return {
            Enter: ({ editor }) => {
                const { $head, empty } = editor.state.selection;
                if (!empty || $head.parent.type !== this.type || !$head.node(-1).attrs.open) return false;
                const content = $head.node(-1).lastChild;
                if (content.childCount !== 1 || content.firstChild.type.name !== 'paragraph' || content.firstChild.content.size) return false;
                return editor.commands.setTextSelection($head.after() + 2);
            }
        };
    },
    addAttributes() {
        return {
            level: {
                default: null,
                parseHTML: element => levels.find(level => String(level) === element.dataset.level) ?? null,
                renderHTML: ({ level }) => level ? { 'data-level': level } : {}
            }
        };
    }
});

// Collapsible blocks (Tiptap's details): a title that stays visible and content that folds away. persist stores the
// open state in the HTML, so a block stays open or closed as it was left once the entry is saved.
export const collapsibleBlocks = [
    Details.configure({
        persist: true,
        renderToggleButton: ({ element, isOpen }) => {
            element.setAttribute('aria-label', isOpen ? 'Zuklappen' : 'Aufklappen');
            element.setAttribute('aria-expanded', String(isOpen));
        }
    }),
    Summary,
    DetailsContent
];

export const inCollapsibleTitle = editor => editor.isActive('detailsSummary');

// Text and heading commands set the level when the cursor is in a title, since the title itself cannot become a heading.
export function setTextLevel(chain, editor, level) {
    if (inCollapsibleTitle(editor)) return chain.updateAttributes('detailsSummary', { level });
    return level ? chain.setHeading({ level }) : chain.setParagraph();
}

// Turns the block at the cursor into an open collapsible block whose title is the block's text. Where a block cannot
// be replaced (the first paragraph of a list item), an empty collapsible block goes right after it instead.
export const setCollapsible = level => ({ state, tr, dispatch }) => {
    const { $from } = tr.selection;
    const block = $from.parent;
    if (!block.isTextblock || block.type.spec.code || $from.depth < 1) return false;
    const { details, detailsSummary, detailsContent, paragraph } = state.schema.nodes;
    const parent = $from.node(-1), index = $from.index(-1);
    const replace = parent.canReplaceWith(index, index + 1, details);
    if (!replace && !parent.canReplaceWith(index + 1, index + 1, details)) return false;
    if (dispatch) {
        const title = [];
        if (replace) block.content.forEach(node => { if (node.isText) title.push(node); });
        const start = replace ? $from.before() : $from.after();
        tr.replaceWith(start, replace ? $from.after() : start, details.create({ open: true }, [
            detailsSummary.create({ level }, title),
            detailsContent.create(null, paragraph.create())
        ]));
        const titleEnd = start + 2 + title.reduce((size, node) => size + node.nodeSize, 0);
        tr.setSelection(TextSelection.create(tr.doc, titleEnd)).scrollIntoView();
    }
    return true;
};
