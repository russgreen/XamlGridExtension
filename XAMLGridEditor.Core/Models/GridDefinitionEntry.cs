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
}
