using System.Collections.Generic;

namespace XAMLGridEditor.Core;

/// <summary>
/// Represents a parsed &lt;Grid&gt; element found in a XAML document.
/// </summary>
public class GridInfo
{
    /// <summary>Absolute character offset where the &lt;Grid …&gt; opening tag starts.</summary>
    public int StartOffset { get; set; }

    /// <summary>Absolute character offset just after the &lt;/Grid&gt; closing tag.</summary>
    public int EndOffset { get; set; }

    /// <summary>Defined row entries (RowDefinition elements).</summary>
    public List<GridDefinitionEntry> Rows { get; set; } = new();

    /// <summary>Defined column entries (ColumnDefinition elements).</summary>
    public List<GridDefinitionEntry> Columns { get; set; } = new();

    /// <summary>Direct child elements placed inside this grid.</summary>
    public List<GridChildInfo> Children { get; set; } = new();

    /// <summary>Number of rows (at least 1 even when no RowDefinitions are present).</summary>
    public int RowCount => Rows.Count == 0 ? 1 : Rows.Count;

    /// <summary>Number of columns (at least 1 even when no ColumnDefinitions are present).</summary>
    public int ColumnCount => Columns.Count == 0 ? 1 : Columns.Count;
}
