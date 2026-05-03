/**
 * XAML Grid parser — TypeScript port of XAMLGridEditor.Core.XamlGridParser.
 *
 * Namespace-agnostic: matches elements by local name so WPF, WinUI, Avalonia,
 * MAUI and other XAML dialects are all supported.
 */

import { GridInfo, GridDefinitionEntry, GridChildInfo } from './models';

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Returns the innermost <Grid> whose source span contains caretOffset (0-based).
 * Returns null when the caret is not inside any grid, or when the XAML cannot
 * be parsed.
 */
export function findGridAtOffset(xaml: string, caretOffset: number): GridInfo | null {
    const grids = parseAllGrids(xaml);
    const candidates = grids.filter(g => g.startOffset <= caretOffset && caretOffset <= g.endOffset);
    if (candidates.length === 0) { return null; }
    candidates.sort((a, b) => (a.endOffset - a.startOffset) - (b.endOffset - b.startOffset));
    return candidates[0];
}

/**
 * Parses all <Grid> elements in the given XAML text and returns them in
 * document order. Returns an empty array when the XAML cannot be parsed.
 */
export function parseAllGrids(xaml: string): GridInfo[] {
    const grids: GridInfo[] = [];
    // Match <Grid followed by whitespace, '>', or '/>' (not GridView, GridSplitter, etc.)
    // Also handles namespace-prefixed grids like <local:Grid>
    const gridPattern = /<(?:[a-zA-Z_][\w.-]*:)?Grid(?=[\s>/])/g;
    let m: RegExpExecArray | null;
    while ((m = gridPattern.exec(xaml)) !== null) {
        const grid = parseGridElement(xaml, m.index);
        if (grid) { grids.push(grid); }
    }
    return grids;
}

// ---------------------------------------------------------------------------
// Internal tag scanning
// ---------------------------------------------------------------------------

interface TagInfo {
    localName: string;
    attributes: Map<string, string>;
    selfClosing: boolean;
    /** Offset just past the closing '>' of the opening tag. */
    tagEnd: number;
}

interface ChildScanResult {
    startOffset: number;
    endOffset: number;
    localName: string;
    attributes: Map<string, string>;
    selfClosing: boolean;
    tagEnd: number;
}

/**
 * Parses the opening tag at `start` (must point to '<').
 * Returns null if the character at `start` is not '<' or is a closing/comment/PI tag.
 */
function scanOpenTag(xaml: string, start: number): TagInfo | null {
    if (start < 0 || start >= xaml.length || xaml[start] !== '<') { return null; }
    let i = start + 1;
    if (i >= xaml.length) { return null; }
    if (xaml[i] === '/' || xaml[i] === '!' || xaml[i] === '?') { return null; }

    // Read element name (including optional namespace prefix)
    const nameStart = i;
    while (i < xaml.length && xaml[i] !== ' ' && xaml[i] !== '\t' && xaml[i] !== '\r' &&
           xaml[i] !== '\n' && xaml[i] !== '>' && xaml[i] !== '/') {
        i++;
    }
    if (i === nameStart) { return null; }

    const fullName = xaml.substring(nameStart, i);
    const colonIdx = fullName.lastIndexOf(':');
    const localName = colonIdx >= 0 ? fullName.substring(colonIdx + 1) : fullName;

    const attributes = new Map<string, string>();

    while (i < xaml.length) {
        // Skip whitespace
        while (i < xaml.length && (xaml[i] === ' ' || xaml[i] === '\t' || xaml[i] === '\r' || xaml[i] === '\n')) {
            i++;
        }
        if (i >= xaml.length) { return null; }

        if (xaml[i] === '>') {
            i++;
            return { localName, attributes, selfClosing: false, tagEnd: i };
        }
        if (xaml[i] === '/' && i + 1 < xaml.length && xaml[i + 1] === '>') {
            i += 2;
            return { localName, attributes, selfClosing: true, tagEnd: i };
        }

        // Read attribute name
        const attrStart = i;
        while (i < xaml.length && xaml[i] !== '=' && xaml[i] !== ' ' && xaml[i] !== '\t' &&
               xaml[i] !== '\r' && xaml[i] !== '\n' && xaml[i] !== '>' && xaml[i] !== '/') {
            i++;
        }
        if (i === attrStart) { i++; continue; }

        const attrName = xaml.substring(attrStart, i);

        // Skip whitespace
        while (i < xaml.length && (xaml[i] === ' ' || xaml[i] === '\t' || xaml[i] === '\r' || xaml[i] === '\n')) {
            i++;
        }
        if (i >= xaml.length) { break; }
        if (xaml[i] !== '=') { continue; }
        i++; // skip '='

        // Skip whitespace
        while (i < xaml.length && (xaml[i] === ' ' || xaml[i] === '\t' || xaml[i] === '\r' || xaml[i] === '\n')) {
            i++;
        }
        if (i >= xaml.length) { break; }

        const quote = xaml[i];
        if (quote !== '"' && quote !== "'") { continue; }
        i++; // skip opening quote

        const valStart = i;
        while (i < xaml.length && xaml[i] !== quote) { i++; }
        const value = xaml.substring(valStart, i);
        if (i < xaml.length) { i++; } // skip closing quote

        attributes.set(attrName, value);
    }
    return null;
}

/**
 * Finds the offset just past the '>' that closes the opening tag starting at
 * `start`, respecting quoted attribute values. Returns -1 on failure.
 */
export function findTagClose(xaml: string, start: number): number {
    let inQuote = false;
    let quoteChar = '"';
    for (let i = start; i < xaml.length; i++) {
        const c = xaml[i];
        if (inQuote) {
            if (c === quoteChar) { inQuote = false; }
        } else {
            if (c === '"' || c === "'") { inQuote = true; quoteChar = c; }
            else if (c === '>') { return i + 1; }
        }
    }
    return -1;
}

/**
 * Starting from startOffset (expected to point at '<'), finds the offset just
 * after the matching closing tag or self-closing '/>'. Returns -1 on failure.
 */
export function findElementEnd(xaml: string, startOffset: number): number {
    if (startOffset < 0 || startOffset >= xaml.length || xaml[startOffset] !== '<') {
        return -1;
    }

    let depth = 0;
    let i = startOffset;

    while (i < xaml.length) {
        if (xaml[i] !== '<') { i++; continue; }

        if (i + 1 < xaml.length && xaml[i + 1] === '/') {
            // Closing tag
            const gtPos = xaml.indexOf('>', i);
            if (gtPos < 0) { return -1; }
            depth--;
            i = gtPos + 1;
            if (depth === 0) { return i; }
        } else if (i + 1 < xaml.length && xaml[i + 1] === '!') {
            // Comment or CDATA — skip to next '>'
            const gtPos = xaml.indexOf('>', i);
            if (gtPos < 0) { return -1; }
            i = gtPos + 1;
        } else if (i + 1 < xaml.length && xaml[i + 1] === '?') {
            // Processing instruction
            const piEnd = xaml.indexOf('?>', i);
            if (piEnd < 0) { return -1; }
            i = piEnd + 2;
        } else {
            // Opening tag — find its '>' respecting quoted attribute values
            const close = findTagClose(xaml, i);
            if (close < 0) { return -1; }
            // close is offset AFTER '>'; check char before '>' for self-closing '/>'
            const selfClosing = close >= 2 && xaml[close - 2] === '/';
            if (!selfClosing) { depth++; }
            i = close;
            if (depth === 0) { return i; } // self-closing at depth 0
        }
    }
    return -1;
}

/**
 * Scans the direct children of an element whose content starts at `contentStart`.
 * Stops at the first closing tag or at `contentEnd`.
 */
function findDirectChildren(xaml: string, contentStart: number, contentEnd: number): ChildScanResult[] {
    const children: ChildScanResult[] = [];
    let pos = contentStart;

    while (pos < contentEnd) {
        // Skip to next '<'
        while (pos < contentEnd && xaml[pos] !== '<') { pos++; }
        if (pos >= contentEnd) { break; }

        // Closing tag means end of parent's content
        if (pos + 1 < xaml.length && xaml[pos + 1] === '/') { break; }

        // Skip comments and PIs
        if (pos + 1 < xaml.length && (xaml[pos + 1] === '!' || xaml[pos + 1] === '?')) {
            const gtPos = xaml.indexOf('>', pos);
            pos = gtPos >= 0 ? gtPos + 1 : contentEnd;
            continue;
        }

        const tag = scanOpenTag(xaml, pos);
        if (!tag) { pos++; continue; }

        const elemEnd = findElementEnd(xaml, pos);
        if (elemEnd < 0) { pos++; continue; }

        children.push({
            startOffset: pos,
            endOffset: elemEnd,
            localName: tag.localName,
            attributes: tag.attributes,
            selfClosing: tag.selfClosing,
            tagEnd: tag.tagEnd,
        });

        // Jump past the entire element to the next sibling
        pos = elemEnd;
    }

    return children;
}

// ---------------------------------------------------------------------------
// Grid element parsing
// ---------------------------------------------------------------------------

/**
 * Finds the [start, end) offsets of the *value* of a named attribute within
 * the tag text that runs from tagStart to tagEnd. Returns null when not found.
 */
function findAttrValueInTag(
    xaml: string, tagStart: number, tagEnd: number, attrName: string
): [number, number] | null {
    let i = tagStart;
    while (i < tagEnd) {
        const idx = xaml.indexOf(attrName, i);
        if (idx < 0 || idx >= tagEnd) { return null; }

        // Must be preceded by whitespace or '<'
        const prev = idx > 0 ? xaml[idx - 1] : '<';
        if (!/[\s<]/.test(prev)) { i = idx + 1; continue; }

        // Must be followed by optional whitespace then '='
        let j = idx + attrName.length;
        while (j < tagEnd && (xaml[j] === ' ' || xaml[j] === '\t')) { j++; }
        if (j >= tagEnd || xaml[j] !== '=') { i = idx + 1; continue; }
        j++; // skip '='
        while (j < tagEnd && (xaml[j] === ' ' || xaml[j] === '\t')) { j++; }
        if (j >= tagEnd) { return null; }

        const quote = xaml[j];
        if (quote !== '"' && quote !== "'") { i = idx + 1; continue; }
        j++; // skip opening quote
        const valStart = j;
        while (j < tagEnd && xaml[j] !== quote) { j++; }
        return [valStart, j];
    }
    return null;
}

/**
 * Parses a shorthand RowDefinitions or ColumnDefinitions attribute on the Grid
 * element and populates info.rows / info.columns plus the span properties.
 */
function parseShorthandDefinitions(
    xaml: string, gridStart: number, tagEnd: number, isRow: boolean, info: GridInfo
): void {
    const attrName = isRow ? 'RowDefinitions' : 'ColumnDefinitions';
    const span = findAttrValueInTag(xaml, gridStart, tagEnd, attrName);
    if (!span) { return; }

    const [valueStart, valueEnd] = span;
    const fullValue = xaml.substring(valueStart, valueEnd);

    if (isRow) {
        info.shorthandRowDefsValueStart = valueStart;
        info.shorthandRowDefsValueEnd = valueEnd;
    } else {
        info.shorthandColDefsValueStart = valueStart;
        info.shorthandColDefsValueEnd = valueEnd;
    }

    let currentOffset = valueStart;
    for (const part of fullValue.split(',')) {
        const leading = part.length - part.trimStart().length;
        const trailing = part.length - part.trimEnd().length;
        const size = part.trim() || '*';

        const entry: GridDefinitionEntry = {
            size,
            startOffset: currentOffset + leading,
            endOffset: currentOffset + part.length - trailing,
            isShorthand: true,
        };

        if (isRow) { info.rows.push(entry); } else { info.columns.push(entry); }
        currentOffset += part.length + 1; // +1 for the comma
    }
}

function parseGridElement(xaml: string, gridStart: number): GridInfo | null {
    const tag = scanOpenTag(xaml, gridStart);
    if (!tag) { return null; }

    const gridEnd = findElementEnd(xaml, gridStart);
    if (gridEnd < 0) { return null; }

    const info: GridInfo = {
        startOffset: gridStart,
        endOffset: gridEnd,
        rows: [],
        columns: [],
        children: [],
    };

    if (tag.selfClosing) { return info; }

    const directChildren = findDirectChildren(xaml, tag.tagEnd, gridEnd);

    for (const child of directChildren) {
        if (child.localName === 'Grid.RowDefinitions') {
            if (!child.selfClosing) {
                const rowDefs = findDirectChildren(xaml, child.tagEnd, child.endOffset);
                for (const rd of rowDefs) {
                    if (rd.localName === 'RowDefinition') {
                        info.rows.push(buildDefinitionEntry(rd));
                    }
                }
            }
        } else if (child.localName === 'Grid.ColumnDefinitions') {
            if (!child.selfClosing) {
                const colDefs = findDirectChildren(xaml, child.tagEnd, child.endOffset);
                for (const cd of colDefs) {
                    if (cd.localName === 'ColumnDefinition') {
                        info.columns.push(buildDefinitionEntry(cd, false));
                    }
                }
            }
        } else if (!child.localName.includes('.')) {
            // Regular child element (skip property elements like Grid.Background)
            info.children.push(buildChildInfo(child));
        }
    }

    // Shorthand attribute syntax: RowDefinitions="*,Auto" / ColumnDefinitions="*,200"
    // Only used when no child element definitions were found (prefer element form).
    if (info.rows.length === 0 && tag.attributes.has('RowDefinitions')) {
        parseShorthandDefinitions(xaml, gridStart, tag.tagEnd, true, info);
    }
    if (info.columns.length === 0 && tag.attributes.has('ColumnDefinitions')) {
        parseShorthandDefinitions(xaml, gridStart, tag.tagEnd, false, info);
    }

    return info;
}

function buildDefinitionEntry(child: ChildScanResult, isRow = true): GridDefinitionEntry {
    const sizeAttr = isRow ? 'Height' : 'Width';
    return {
        size: child.attributes.get(sizeAttr) ?? '*',
        startOffset: child.startOffset,
        endOffset: child.endOffset,
    };
}

function buildChildInfo(child: ChildScanResult): GridChildInfo {
    const gridRow = child.attributes.get('Grid.Row');
    const gridCol = child.attributes.get('Grid.Column');
    const gridRowSpan = child.attributes.get('Grid.RowSpan');
    const gridColSpan = child.attributes.get('Grid.ColumnSpan');

    const row = gridRow !== undefined ? (parseInt(gridRow, 10) || 0) : 0;
    const col = gridCol !== undefined ? (parseInt(gridCol, 10) || 0) : 0;
    const rowSpan = gridRowSpan !== undefined ? (parseInt(gridRowSpan, 10) || 1) : 1;
    const colSpan = gridColSpan !== undefined ? (parseInt(gridColSpan, 10) || 1) : 1;

    return {
        elementName: child.localName,
        row,
        column: col,
        rowSpan,
        columnSpan: colSpan,
        hasExplicitRow: gridRow !== undefined,
        hasExplicitColumn: gridCol !== undefined,
        hasExplicitRowSpan: gridRowSpan !== undefined,
        hasExplicitColumnSpan: gridColSpan !== undefined,
        startOffset: child.startOffset,
        endOffset: child.endOffset,
    };
}
