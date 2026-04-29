using System.Collections.Generic;

namespace XamlGridExtension.Models;

/// <summary>Represents a parsed XAML Grid element and its structural information.</summary>
public sealed class GridInfo
{
    /// <summary>Number of row definitions (defaults to 1 if RowDefinitions is absent).</summary>
    public int RowCount { get; set; }

    /// <summary>Number of column definitions (defaults to 1 if ColumnDefinitions is absent).</summary>
    public int ColumnCount { get; set; }

    /// <summary>Raw Height values from RowDefinitions (e.g., "Auto", "*", "100").</summary>
    public List<string> RowHeights { get; set; } = new();

    /// <summary>Raw Width values from ColumnDefinitions (e.g., "Auto", "*", "100").</summary>
    public List<string> ColumnWidths { get; set; } = new();

    /// <summary>Child elements that carry Grid.Row / Grid.Column attributes.</summary>
    public List<GridChildInfo> Children { get; set; } = new();
}

/// <summary>Represents a direct child of a Grid element with its placement.</summary>
public sealed class GridChildInfo
{
    public string ElementName { get; set; } = string.Empty;
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
}
