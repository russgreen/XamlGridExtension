using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XAMLGridEditor.Core;

/// <summary>
/// Inserts and removes RowDefinition / ColumnDefinition entries in a XAML Grid
/// and updates all child element Grid.Row / Grid.Column / Grid.RowSpan /
/// Grid.ColumnSpan attributes accordingly.
/// </summary>
public static class GridManipulator
{
    // -----------------------------------------------------------------------
    // Insert operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// Inserts a new RowDefinition with <paramref name="height"/> before row
    /// <paramref name="beforeIndex"/> in the grid described by <paramref name="grid"/>.
    /// </summary>
    /// <param name="xaml">Full XAML document text.</param>
    /// <param name="grid">Parsed grid to operate on.</param>
    /// <param name="beforeIndex">0-based row index to insert before. Use <c>grid.RowCount</c> to append.</param>
    /// <param name="height">Height value for the new RowDefinition (default <c>"*"</c>).</param>
    /// <returns>Updated XAML text.</returns>
    public static string InsertRow(string xaml, GridInfo grid, int beforeIndex, string height = "*")
    {
        beforeIndex = Math.Max(0, Math.Min(beforeIndex, grid.RowCount));

        // 1. Update child attributes first (offsets not yet shifted).
        xaml = GridChildAttributeUpdater.InsertRowAttributes(xaml, grid.Children, beforeIndex);

        // Re-parse so we have fresh offsets after the attribute update.
        grid = XamlGridParser.FindGridAtOffset(xaml, grid.StartOffset + 1) ?? grid;

        // 2. Insert the RowDefinition entry.
        xaml = InsertDefinitionEntry(xaml, grid, isRow: true, beforeIndex: beforeIndex, size: height);

        return xaml;
    }

    /// <summary>
    /// Inserts a new ColumnDefinition with <paramref name="width"/> before column
    /// <paramref name="beforeIndex"/> in the grid described by <paramref name="grid"/>.
    /// </summary>
    public static string InsertColumn(string xaml, GridInfo grid, int beforeIndex, string width = "*")
    {
        beforeIndex = Math.Max(0, Math.Min(beforeIndex, grid.ColumnCount));

        xaml = GridChildAttributeUpdater.InsertColumnAttributes(xaml, grid.Children, beforeIndex);

        grid = XamlGridParser.FindGridAtOffset(xaml, grid.StartOffset + 1) ?? grid;

        xaml = InsertDefinitionEntry(xaml, grid, isRow: false, beforeIndex: beforeIndex, size: width);

        return xaml;
    }

    // -----------------------------------------------------------------------
    // Remove operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// Removes the RowDefinition at <paramref name="removeIndex"/> from the grid.
    /// Cannot remove the last row definition.
    /// </summary>
    /// <param name="xaml">Full XAML document text.</param>
    /// <param name="grid">Parsed grid to operate on.</param>
    /// <param name="removeIndex">0-based row index to remove.</param>
    /// <param name="warnings">Warning messages for elements affected by the removal.</param>
    /// <returns>Updated XAML text.</returns>
    public static string RemoveRow(string xaml, GridInfo grid, int removeIndex, out List<string> warnings)
    {
        warnings = new List<string>();

        if (grid.Rows.Count <= 1)
        {
            warnings.Add("Cannot remove the only row definition.");
            return xaml;
        }

        removeIndex = Math.Max(0, Math.Min(removeIndex, grid.Rows.Count - 1));

        // 1. Update child attributes first.
        xaml = GridChildAttributeUpdater.RemoveRowAttributes(xaml, grid.Children, removeIndex, out warnings);

        grid = XamlGridParser.FindGridAtOffset(xaml, grid.StartOffset + 1) ?? grid;

        // 2. Remove the definition entry.
        xaml = RemoveDefinitionEntry(xaml, grid, isRow: true, removeIndex: removeIndex);

        return xaml;
    }

    /// <summary>
    /// Removes the ColumnDefinition at <paramref name="removeIndex"/> from the grid.
    /// Cannot remove the last column definition.
    /// </summary>
    public static string RemoveColumn(string xaml, GridInfo grid, int removeIndex, out List<string> warnings)
    {
        warnings = new List<string>();

        if (grid.Columns.Count <= 1)
        {
            warnings.Add("Cannot remove the only column definition.");
            return xaml;
        }

        removeIndex = Math.Max(0, Math.Min(removeIndex, grid.Columns.Count - 1));

        xaml = GridChildAttributeUpdater.RemoveColumnAttributes(xaml, grid.Children, removeIndex, out warnings);

        grid = XamlGridParser.FindGridAtOffset(xaml, grid.StartOffset + 1) ?? grid;

        xaml = RemoveDefinitionEntry(xaml, grid, isRow: false, removeIndex: removeIndex);

        return xaml;
    }

    // -----------------------------------------------------------------------
    // Size-change operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// Updates the Height attribute of the RowDefinition at <paramref name="rowIndex"/>.
    /// </summary>
    public static string SetRowSize(string xaml, GridInfo grid, int rowIndex, string newHeight)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return xaml;
        return SetDefinitionSize(xaml, grid.Rows[rowIndex], "Height", newHeight);
    }

    /// <summary>
    /// Updates the Width attribute of the ColumnDefinition at <paramref name="colIndex"/>.
    /// </summary>
    public static string SetColumnSize(string xaml, GridInfo grid, int colIndex, string newWidth)
    {
        if (colIndex < 0 || colIndex >= grid.Columns.Count) return xaml;
        return SetDefinitionSize(xaml, grid.Columns[colIndex], "Width", newWidth);
    }

    private static string SetDefinitionSize(string xaml, GridDefinitionEntry entry, string attrName, string newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue)) return xaml;

        string elementText = xaml.Substring(entry.StartOffset, entry.EndOffset - entry.StartOffset);

        // Try to replace an existing attribute value.
        var match = Regex.Match(elementText, attrName + "=\"([^\"]*)\"");
        if (match.Success)
        {
            int valueStart = entry.StartOffset + match.Groups[1].Index;
            int valueLen   = match.Groups[1].Length;
            return xaml.Substring(0, valueStart) + newValue + xaml.Substring(valueStart + valueLen);
        }

        // Attribute doesn't exist – insert before the closing '>' or '/>'.
        int closeIdx = elementText.IndexOf('>');
        if (closeIdx < 0) return xaml;
        bool selfClose = closeIdx > 0 && elementText[closeIdx - 1] == '/';
        int insertAt = entry.StartOffset + (selfClose ? closeIdx - 1 : closeIdx);
        return xaml.Substring(0, insertAt) + $" {attrName}=\"{newValue}\"" + xaml.Substring(insertAt);
    }

    // -----------------------------------------------------------------------
    // Definition entry manipulation
    // -----------------------------------------------------------------------

    private static string InsertDefinitionEntry(string xaml, GridInfo grid, bool isRow, int beforeIndex, string size)
    {
        var definitions = isRow ? grid.Rows : grid.Columns;
        string sizeAttr = isRow ? "Height" : "Width";
        string entryTag = isRow ? "RowDefinition" : "ColumnDefinition";
        string containerTag = isRow ? "Grid.RowDefinitions" : "Grid.ColumnDefinitions";

        string newEntry = $"<{entryTag} {sizeAttr}=\"{size}\"/>";

        if (definitions.Count == 0)
        {
            // No definitions block exists – we need to create it inside the Grid.
            return InsertDefinitionsBlock(xaml, grid, containerTag, newEntry, sizeAttr, isRow);
        }

        if (beforeIndex >= definitions.Count)
        {
            // Append after the last definition.
            var last = definitions[definitions.Count - 1];
            int insertAt = last.EndOffset;
            string indent = GetIndent(xaml, last.StartOffset);
            return xaml.Substring(0, insertAt) + "\r\n" + indent + newEntry + xaml.Substring(insertAt);
        }
        else
        {
            // Insert before the definition at beforeIndex.
            var target = definitions[beforeIndex];
            int insertAt = target.StartOffset;
            string indent = GetIndent(xaml, target.StartOffset);
            return xaml.Substring(0, insertAt) + newEntry + "\r\n" + indent + xaml.Substring(insertAt);
        }
    }

    private static string InsertDefinitionsBlock(string xaml, GridInfo grid,
        string containerTag, string newEntry, string sizeAttr, bool isRow)
    {
        // Find the end of the Grid opening tag so we can insert the block immediately after.
        int gridTagEnd = FindOpeningTagEnd(xaml, grid.StartOffset);
        if (gridTagEnd < 0) return xaml;

        string indent = GetIndent(xaml, grid.StartOffset);
        string innerIndent = indent + "    ";

        string block =
            $"\r\n{innerIndent}<{containerTag}>" +
            $"\r\n{innerIndent}    {newEntry}" +
            $"\r\n{innerIndent}</{containerTag}>";

        return xaml.Substring(0, gridTagEnd) + block + xaml.Substring(gridTagEnd);
    }

    private static string RemoveDefinitionEntry(string xaml, GridInfo grid, bool isRow, int removeIndex)
    {
        var definitions = isRow ? grid.Rows : grid.Columns;
        if (definitions.Count == 0 || removeIndex >= definitions.Count)
            return xaml;

        var entry = definitions[removeIndex];

        // Find the full line containing this entry (including leading whitespace and the newline).
        int lineStart = FindLineStart(xaml, entry.StartOffset);
        int lineEnd = FindLineEnd(xaml, entry.EndOffset - 1);

        return xaml.Substring(0, lineStart) + xaml.Substring(lineEnd);
    }

    // -----------------------------------------------------------------------
    // Text helpers
    // -----------------------------------------------------------------------

    private static int FindOpeningTagEnd(string xaml, int elementOffset)
    {
        bool inQuote = false;
        char quoteChar = '"';
        for (int i = elementOffset; i < xaml.Length; i++)
        {
            char c = xaml[i];
            if (inQuote) { if (c == quoteChar) inQuote = false; }
            else
            {
                if (c == '"' || c == '\'') { inQuote = true; quoteChar = c; }
                else if (c == '>')
                {
                    // Check for self-closing: if it ends with '/>',
                    // the element has no children so there's nothing to insert.
                    return i + 1;
                }
            }
        }
        return -1;
    }

    private static string GetIndent(string xaml, int offset)
    {
        int lineStart = FindLineStart(xaml, offset);
        int i = lineStart;
        while (i < offset && (xaml[i] == ' ' || xaml[i] == '\t'))
            i++;
        return xaml.Substring(lineStart, i - lineStart);
    }

    private static int FindLineStart(string xaml, int offset)
    {
        int i = offset - 1;
        while (i >= 0 && xaml[i] != '\n') i--;
        return i + 1;
    }

    private static int FindLineEnd(string xaml, int offset)
    {
        int i = offset;
        while (i < xaml.Length && xaml[i] != '\n') i++;
        if (i < xaml.Length) i++; // include the '\n'
        return i;
    }
}
