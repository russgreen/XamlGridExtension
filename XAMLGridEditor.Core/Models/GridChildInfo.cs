namespace XAMLGridEditor.Core;

/// <summary>
/// Represents a child element inside a Grid with its placement attributes.
/// </summary>
public class GridChildInfo
{
    /// <summary>Local name of the element (e.g. "TextBlock").</summary>
    public string ElementName { get; set; } = string.Empty;

    /// <summary>Grid.Row value (0-based). Defaults to 0 when the attribute is absent.</summary>
    public int Row { get; set; }

    /// <summary>Grid.Column value (0-based). Defaults to 0 when the attribute is absent.</summary>
    public int Column { get; set; }

    /// <summary>Grid.RowSpan value. Defaults to 1 when the attribute is absent.</summary>
    public int RowSpan { get; set; } = 1;

    /// <summary>Grid.ColumnSpan value. Defaults to 1 when the attribute is absent.</summary>
    public int ColumnSpan { get; set; } = 1;

    /// <summary>Whether the Grid.Row attribute was explicitly present in the source.</summary>
    public bool HasExplicitRow { get; set; }

    /// <summary>Whether the Grid.Column attribute was explicitly present in the source.</summary>
    public bool HasExplicitColumn { get; set; }

    /// <summary>Whether the Grid.RowSpan attribute was explicitly present in the source.</summary>
    public bool HasExplicitRowSpan { get; set; }

    /// <summary>Whether the Grid.ColumnSpan attribute was explicitly present in the source.</summary>
    public bool HasExplicitColumnSpan { get; set; }

    /// <summary>Absolute character offset of the start of this element's opening tag.</summary>
    public int StartOffset { get; set; }

    /// <summary>Absolute character offset just after this element's closing tag (or self-closing />).</summary>
    public int EndOffset { get; set; }
}
