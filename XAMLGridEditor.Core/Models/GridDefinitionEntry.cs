namespace XAMLGridEditor.Core;

/// <summary>
/// Represents a single RowDefinition or ColumnDefinition entry in a Grid.
/// </summary>
public class GridDefinitionEntry
{
    /// <summary>The Height (for rows) or Width (for columns) value, e.g. "Auto", "*", "200".</summary>
    public string Size { get; set; } = "*";

    /// <summary>Absolute character offset of the start of this definition element's opening tag.</summary>
    public int StartOffset { get; set; }

    /// <summary>Absolute character offset just after the end of this definition element.</summary>
    public int EndOffset { get; set; }

    /// <summary>
    /// True when this entry was parsed from a shorthand attribute on the Grid element
    /// (e.g. <c>RowDefinitions="*, Auto"</c>) rather than a child
    /// <c>&lt;RowDefinition&gt;</c> element.
    /// When true, <see cref="StartOffset"/> and <see cref="EndOffset"/> bracket the
    /// individual value within the attribute string; the manipulator replaces that
    /// span directly instead of searching for an XML attribute.
    /// </summary>
    public bool IsShorthand { get; set; }
}
