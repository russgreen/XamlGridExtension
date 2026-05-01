/**
 * Grid manipulator — TypeScript port of XAMLGridEditor.Core.GridManipulator
 * and XAMLGridEditor.Core.GridChildAttributeUpdater.
 *
 * All functions are pure: they take the XAML string and return an updated copy.
 */

import { GridInfo, GridDefinitionEntry, GridChildInfo, rowCount, columnCount } from './models';
import { findGridAtOffset } from './xamlParser';
import { findTagClose } from './xamlParser';

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export function insertRow(xaml: string, grid: GridInfo, beforeIndex: number, height = '*'): string {
    beforeIndex = Math.max(0, Math.min(beforeIndex, rowCount(grid)));
    xaml = adjustAttributes(xaml, grid.children, true, true, beforeIndex);
    grid = findGridAtOffset(xaml, grid.startOffset + 1) ?? grid;
    return insertDefinitionEntry(xaml, grid, true, beforeIndex, height);
}

export function insertColumn(xaml: string, grid: GridInfo, beforeIndex: number, width = '*'): string {
    beforeIndex = Math.max(0, Math.min(beforeIndex, columnCount(grid)));
    xaml = adjustAttributes(xaml, grid.children, false, true, beforeIndex);
    grid = findGridAtOffset(xaml, grid.startOffset + 1) ?? grid;
    return insertDefinitionEntry(xaml, grid, false, beforeIndex, width);
}

export function removeRow(xaml: string, grid: GridInfo, removeIndex: number): { xaml: string; warnings: string[] } {
    if (grid.rows.length <= 1) {
        return { xaml, warnings: ['Cannot remove the only row definition.'] };
    }
    removeIndex = Math.max(0, Math.min(removeIndex, grid.rows.length - 1));
    const { xaml: updated, warnings } = adjustAttributesWithWarnings(xaml, grid.children, true, removeIndex);
    xaml = updated;
    grid = findGridAtOffset(xaml, grid.startOffset + 1) ?? grid;
    return { xaml: removeDefinitionEntry(xaml, grid, true, removeIndex), warnings };
}

export function removeColumn(xaml: string, grid: GridInfo, removeIndex: number): { xaml: string; warnings: string[] } {
    if (grid.columns.length <= 1) {
        return { xaml, warnings: ['Cannot remove the only column definition.'] };
    }
    removeIndex = Math.max(0, Math.min(removeIndex, grid.columns.length - 1));
    const { xaml: updated, warnings } = adjustAttributesWithWarnings(xaml, grid.children, false, removeIndex);
    xaml = updated;
    grid = findGridAtOffset(xaml, grid.startOffset + 1) ?? grid;
    return { xaml: removeDefinitionEntry(xaml, grid, false, removeIndex), warnings };
}

export function setRowSize(xaml: string, grid: GridInfo, rowIndex: number, newHeight: string): string {
    if (rowIndex < 0 || rowIndex >= grid.rows.length) { return xaml; }
    return setDefinitionSize(xaml, grid.rows[rowIndex], 'Height', newHeight);
}

export function setColumnSize(xaml: string, grid: GridInfo, colIndex: number, newWidth: string): string {
    if (colIndex < 0 || colIndex >= grid.columns.length) { return xaml; }
    return setDefinitionSize(xaml, grid.columns[colIndex], 'Width', newWidth);
}

// ---------------------------------------------------------------------------
// Definition entry insertion / removal
// ---------------------------------------------------------------------------

function insertDefinitionEntry(
    xaml: string, grid: GridInfo, isRow: boolean, beforeIndex: number, size: string
): string {
    const definitions = isRow ? grid.rows : grid.columns;
    const sizeAttr = isRow ? 'Height' : 'Width';
    const entryTag = isRow ? 'RowDefinition' : 'ColumnDefinition';
    const containerTag = isRow ? 'Grid.RowDefinitions' : 'Grid.ColumnDefinitions';
    const newEntry = `<${entryTag} ${sizeAttr}="${size}"/>`;

    if (definitions.length === 0) {
        return insertDefinitionsBlock(xaml, grid, containerTag, newEntry);
    }
    if (beforeIndex >= definitions.length) {
        const last = definitions[definitions.length - 1];
        const indent = getIndent(xaml, last.startOffset);
        return xaml.substring(0, last.endOffset) + '\r\n' + indent + newEntry + xaml.substring(last.endOffset);
    } else {
        const target = definitions[beforeIndex];
        const indent = getIndent(xaml, target.startOffset);
        return xaml.substring(0, target.startOffset) + newEntry + '\r\n' + indent + xaml.substring(target.startOffset);
    }
}

function insertDefinitionsBlock(xaml: string, grid: GridInfo, containerTag: string, newEntry: string): string {
    const gridTagEnd = findTagClose(xaml, grid.startOffset);
    if (gridTagEnd < 0) { return xaml; }
    const indent = getIndent(xaml, grid.startOffset);
    const inner = indent + '    ';
    const block =
        `\r\n${inner}<${containerTag}>` +
        `\r\n${inner}    ${newEntry}` +
        `\r\n${inner}</${containerTag}>`;
    return xaml.substring(0, gridTagEnd) + block + xaml.substring(gridTagEnd);
}

function removeDefinitionEntry(xaml: string, grid: GridInfo, isRow: boolean, removeIndex: number): string {
    const definitions = isRow ? grid.rows : grid.columns;
    if (definitions.length === 0 || removeIndex >= definitions.length) { return xaml; }
    const entry = definitions[removeIndex];
    const lineStart = findLineStart(xaml, entry.startOffset);
    const lineEnd = findLineEnd(xaml, entry.endOffset - 1);
    return xaml.substring(0, lineStart) + xaml.substring(lineEnd);
}

// ---------------------------------------------------------------------------
// Size attribute update
// ---------------------------------------------------------------------------

function setDefinitionSize(
    xaml: string, entry: GridDefinitionEntry, attrName: string, newValue: string
): string {
    if (!newValue.trim()) { return xaml; }
    const [s, e] = findAttributeValueSpan(xaml, entry.startOffset, attrName);
    if (s >= 0) {
        return xaml.substring(0, s) + newValue + xaml.substring(e);
    }
    // Attribute missing — insert before the closing '>' or '/>'
    const elementText = xaml.substring(entry.startOffset, entry.endOffset);
    const closeIdx = elementText.indexOf('>');
    if (closeIdx < 0) { return xaml; }
    const selfClose = closeIdx > 0 && elementText[closeIdx - 1] === '/';
    const insertAt = entry.startOffset + (selfClose ? closeIdx - 1 : closeIdx);
    return xaml.substring(0, insertAt) + ` ${attrName}="${newValue}"` + xaml.substring(insertAt);
}

// ---------------------------------------------------------------------------
// Child attribute adjustment (insert / remove rows and columns)
// ---------------------------------------------------------------------------

type Replacement = { start: number; end: number; newText: string };

function adjustAttributes(
    xaml: string, children: GridChildInfo[], isRow: boolean, isInsert: boolean, targetIndex: number
): string {
    return adjustAttributesInternal(xaml, children, isRow, isInsert, targetIndex).xaml;
}

function adjustAttributesWithWarnings(
    xaml: string, children: GridChildInfo[], isRow: boolean, targetIndex: number
): { xaml: string; warnings: string[] } {
    return adjustAttributesInternal(xaml, children, isRow, false, targetIndex);
}

function adjustAttributesInternal(
    xaml: string,
    children: GridChildInfo[],
    isRow: boolean,
    isInsert: boolean,
    targetIndex: number
): { xaml: string; warnings: string[] } {
    const warnings: string[] = [];
    const replacements: Replacement[] = [];

    for (const child of children) {
        const position = isRow ? child.row : child.column;
        const span = isRow ? child.rowSpan : child.columnSpan;
        const hasExplicitPos = isRow ? child.hasExplicitRow : child.hasExplicitColumn;
        const hasExplicitSpan = isRow ? child.hasExplicitRowSpan : child.hasExplicitColumnSpan;
        const posAttr = isRow ? 'Grid.Row' : 'Grid.Column';
        const spanAttr = isRow ? 'Grid.RowSpan' : 'Grid.ColumnSpan';

        if (isInsert) {
            if (position >= targetIndex) {
                const newPos = position + 1;
                if (hasExplicitPos) {
                    const [s, e] = findAttributeValueSpan(xaml, child.startOffset, posAttr);
                    if (s >= 0) { replacements.push({ start: s, end: e, newText: String(newPos) }); }
                } else {
                    replacements.push(insertAttributeAfterTagName(xaml, child.startOffset, posAttr, String(newPos)));
                }
            }
            // Grow span if the insertion point falls inside the span
            if (position < targetIndex && position + span > targetIndex) {
                const newSpan = span + 1;
                if (hasExplicitSpan) {
                    const [s, e] = findAttributeValueSpan(xaml, child.startOffset, spanAttr);
                    if (s >= 0) { replacements.push({ start: s, end: e, newText: String(newSpan) }); }
                } else {
                    replacements.push(insertAttributeAfterTagName(xaml, child.startOffset, spanAttr, String(newSpan)));
                }
            }
        } else {
            if (position === targetIndex) {
                warnings.push(
                    `Element '${child.elementName}' at ${posAttr}=${position} is on the removed definition and will move to ${posAttr}=0.`
                );
                if (hasExplicitPos) {
                    const [s, e] = findAttributeValueSpan(xaml, child.startOffset, posAttr);
                    if (s >= 0) { replacements.push({ start: s, end: e, newText: '0' }); }
                }
            } else if (position > targetIndex) {
                const newPos = position - 1;
                if (hasExplicitPos) {
                    const [s, e] = findAttributeValueSpan(xaml, child.startOffset, posAttr);
                    if (s >= 0) { replacements.push({ start: s, end: e, newText: String(newPos) }); }
                }
            }
            // Clamp span if it covered the removed index
            if (position <= targetIndex && targetIndex < position + span && span > 1) {
                const newSpan = Math.max(1, span - 1);
                if (hasExplicitSpan) {
                    const [s, e] = findAttributeValueSpan(xaml, child.startOffset, spanAttr);
                    if (s >= 0) { replacements.push({ start: s, end: e, newText: String(newSpan) }); }
                }
                if (span > 1) {
                    warnings.push(
                        `Element '${child.elementName}' ${spanAttr} reduced from ${span} to ${newSpan} due to row/column removal.`
                    );
                }
            }
        }
    }

    // Apply replacements in reverse document order so offsets stay valid
    replacements.sort((a, b) => b.start - a.start);
    let result = xaml;
    for (const { start, end, newText } of replacements) {
        result = result.substring(0, start) + newText + result.substring(end);
    }
    return { xaml: result, warnings };
}

// ---------------------------------------------------------------------------
// Attribute location helpers
// ---------------------------------------------------------------------------

/**
 * Finds the [start, end) offsets of the *value* of an attribute on the element
 * whose opening tag starts at elementOffset. Returns [-1, -1] when not found.
 */
export function findAttributeValueSpan(
    xaml: string, elementOffset: number, attributeName: string
): [number, number] {
    const tagEndPos = findTagClose(xaml, elementOffset);
    if (tagEndPos < 0) { return [-1, -1]; }

    let i = elementOffset;
    const endPos = tagEndPos;

    while (i < endPos) {
        const attrStart = xaml.indexOf(attributeName, i);
        if (attrStart < 0 || attrStart >= endPos) { return [-1, -1]; }

        // Ensure it's a proper attribute (preceded by whitespace or '<')
        const prevChar = attrStart > 0 ? xaml[attrStart - 1] : '<';
        // Ensure not a prefix of another attribute name (followed by '=' or whitespace)
        const nextCharIdx = attrStart + attributeName.length;
        const nextChar = nextCharIdx < xaml.length ? xaml[nextCharIdx] : '';

        if (!/[\s<]/.test(prevChar) || !/[\s=]/.test(nextChar)) {
            i = attrStart + 1;
            continue;
        }

        // Skip to '='
        let j = nextCharIdx;
        while (j < endPos && (xaml[j] === ' ' || xaml[j] === '\t')) { j++; }
        if (j >= endPos || xaml[j] !== '=') { i = attrStart + 1; continue; }
        j++; // skip '='

        // Skip whitespace
        while (j < endPos && (xaml[j] === ' ' || xaml[j] === '\t')) { j++; }

        if (j >= endPos) { return [-1, -1]; }
        const quote = xaml[j];
        if (quote !== '"' && quote !== "'") { i = attrStart + 1; continue; }
        j++; // skip opening quote

        const valStart = j;
        while (j < endPos && xaml[j] !== quote) { j++; }

        return [valStart, j];
    }
    return [-1, -1];
}

/**
 * Builds a zero-length-replacement tuple that inserts a new attribute
 * immediately after the tag name.
 */
function insertAttributeAfterTagName(
    xaml: string, elementOffset: number, attrName: string, attrValue: string
): Replacement {
    let i = elementOffset + 1; // skip '<'
    while (i < xaml.length && !/[\s>/]/.test(xaml[i])) { i++; }
    return { start: i, end: i, newText: ` ${attrName}="${attrValue}"` };
}

// ---------------------------------------------------------------------------
// Text helpers
// ---------------------------------------------------------------------------

function getIndent(xaml: string, offset: number): string {
    const lineStart = findLineStart(xaml, offset);
    let i = lineStart;
    while (i < offset && (xaml[i] === ' ' || xaml[i] === '\t')) { i++; }
    return xaml.substring(lineStart, i);
}

function findLineStart(xaml: string, offset: number): number {
    let i = offset - 1;
    while (i >= 0 && xaml[i] !== '\n') { i--; }
    return i + 1;
}

function findLineEnd(xaml: string, offset: number): number {
    let i = offset;
    while (i < xaml.length && xaml[i] !== '\n') { i++; }
    if (i < xaml.length) { i++; } // include the '\n'
    return i;
}
