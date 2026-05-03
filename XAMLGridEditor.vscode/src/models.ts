/**
 * Data models — TypeScript port of the XAMLGridEditor.Core models.
 */

export interface GridDefinitionEntry {
    /** Height (rows) or Width (columns) value, e.g. "Auto", "*", "200". */
    size: string;
    /** Absolute character offset of the start of this definition element's opening tag. */
    startOffset: number;
    /** Absolute character offset just after the end of this definition element. */
    endOffset: number;
    /**
     * True when this entry was parsed from a shorthand attribute on the Grid element
     * (e.g. `RowDefinitions="*, Auto"`) rather than a child `<RowDefinition>` element.
     * When true, startOffset/endOffset bracket the individual value within the
     * attribute string so the manipulator can replace it directly.
     */
    isShorthand?: boolean;
}

export interface GridChildInfo {
    elementName: string;
    row: number;
    column: number;
    rowSpan: number;
    columnSpan: number;
    hasExplicitRow: boolean;
    hasExplicitColumn: boolean;
    hasExplicitRowSpan: boolean;
    hasExplicitColumnSpan: boolean;
    /** Absolute character offset of the start of this element's opening tag. */
    startOffset: number;
    /** Absolute character offset just after this element's closing tag. */
    endOffset: number;
}

export interface GridInfo {
    /** Absolute character offset where the <Grid …> opening tag starts. */
    startOffset: number;
    /** Absolute character offset just after the </Grid> closing tag. */
    endOffset: number;
    rows: GridDefinitionEntry[];
    columns: GridDefinitionEntry[];
    children: GridChildInfo[];
    /** Offset of the first character of the RowDefinitions attribute value (inside quotes). */
    shorthandRowDefsValueStart?: number;
    /** Offset just after the last character of the RowDefinitions attribute value. */
    shorthandRowDefsValueEnd?: number;
    /** Offset of the first character of the ColumnDefinitions attribute value (inside quotes). */
    shorthandColDefsValueStart?: number;
    /** Offset just after the last character of the ColumnDefinitions attribute value. */
    shorthandColDefsValueEnd?: number;
}

export function rowCount(grid: GridInfo): number {
    return grid.rows.length === 0 ? 1 : grid.rows.length;
}

export function columnCount(grid: GridInfo): number {
    return grid.columns.length === 0 ? 1 : grid.columns.length;
}
