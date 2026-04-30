using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace XAMLGridEditor.Core;

/// <summary>
/// Parses a XAML document to locate Grid elements and their children.
/// Namespace-agnostic: matches elements by local name so WPF, WinUI,
/// Avalonia, MAUI and other XAML dialects are all supported.
/// </summary>
public static class XamlGridParser
{
    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the innermost <c>&lt;Grid&gt;</c> whose source span contains
    /// <paramref name="caretOffset"/> (0-based absolute character offset).
    /// Returns <c>null</c> when the caret is not inside any grid, or when
    /// the XAML cannot be parsed.
    /// </summary>
    public static GridInfo? FindGridAtOffset(string xaml, int caretOffset)
    {
        var grids = ParseAllGrids(xaml);
        // Pick the innermost (smallest span) grid that contains the caret.
        return grids
            .Where(g => g.StartOffset <= caretOffset && caretOffset <= g.EndOffset)
            .OrderBy(g => g.EndOffset - g.StartOffset)
            .FirstOrDefault();
    }

    /// <summary>
    /// Parses all <c>&lt;Grid&gt;</c> elements in the given XAML text and
    /// returns them as a list (order is document order).
    /// Returns an empty list when the XAML cannot be parsed.
    /// </summary>
    public static List<GridInfo> ParseAllGrids(string xaml)
    {
        var results = new List<GridInfo>();
        try
        {
            // Use an XmlReader that reports line/column positions so we can
            // map each node back to a character offset.
            var lineOffsets = BuildLineOffsetTable(xaml);

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreWhitespace = false
            };

            using var reader = XmlReader.Create(new StringReader(xaml), settings);
            var doc = XDocument.Load(reader, LoadOptions.SetLineInfo);

            foreach (var gridElement in doc.Descendants()
                         .Where(e => e.Name.LocalName == "Grid"))
            {
                var info = BuildGridInfo(gridElement, xaml, lineOffsets);
                if (info != null)
                    results.Add(info);
            }
        }
        catch (XmlException)
        {
            // Malformed XAML – return what we have so far (possibly empty).
        }

        return results;
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private static GridInfo? BuildGridInfo(XElement gridElement, string xaml, int[] lineOffsets)
    {
        if (gridElement is not IXmlLineInfo gridLineInfo || !gridLineInfo.HasLineInfo())
            return null;

        int gridStart = LineColToOffset(lineOffsets, gridLineInfo.LineNumber, gridLineInfo.LinePosition);

        // Find the end offset: walk forward to the matching close tag.
        int gridEnd = FindElementEnd(xaml, gridStart);
        if (gridEnd < 0)
            return null;

        var info = new GridInfo
        {
            StartOffset = gridStart,
            EndOffset = gridEnd
        };

        // RowDefinitions
        var rowDefs = gridElement
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Grid.RowDefinitions");
        if (rowDefs != null)
        {
            foreach (var rowDef in rowDefs.Elements().Where(e => e.Name.LocalName == "RowDefinition"))
            {
                var defEntry = BuildDefinitionEntry(rowDef, lineOffsets, xaml, isRow: true);
                if (defEntry != null)
                    info.Rows.Add(defEntry);
            }
        }

        // ColumnDefinitions
        var colDefs = gridElement
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Grid.ColumnDefinitions");
        if (colDefs != null)
        {
            foreach (var colDef in colDefs.Elements().Where(e => e.Name.LocalName == "ColumnDefinition"))
            {
                var defEntry = BuildDefinitionEntry(colDef, lineOffsets, xaml, isRow: false);
                if (defEntry != null)
                    info.Columns.Add(defEntry);
            }
        }

        // Child elements (direct, excluding property elements like Grid.RowDefinitions)
        foreach (var child in gridElement.Elements()
                     .Where(e => !e.Name.LocalName.StartsWith("Grid.", StringComparison.Ordinal)))
        {
            var childInfo = BuildChildInfo(child, lineOffsets, xaml);
            if (childInfo != null)
                info.Children.Add(childInfo);
        }

        return info;
    }

    private static GridDefinitionEntry? BuildDefinitionEntry(XElement element, int[] lineOffsets, string xaml, bool isRow)
    {
        if (element is not IXmlLineInfo li || !li.HasLineInfo())
            return null;

        int start = LineColToOffset(lineOffsets, li.LineNumber, li.LinePosition);
        int end = FindElementEnd(xaml, start);
        if (end < 0) end = start;

        string sizeAttr = isRow ? "Height" : "Width";
        string size = element.Attribute(sizeAttr)?.Value ?? "*";

        return new GridDefinitionEntry
        {
            Size = size,
            StartOffset = start,
            EndOffset = end
        };
    }

    private static GridChildInfo? BuildChildInfo(XElement element, int[] lineOffsets, string xaml)
    {
        if (element is not IXmlLineInfo li || !li.HasLineInfo())
            return null;

        int start = LineColToOffset(lineOffsets, li.LineNumber, li.LinePosition);
        int end = FindElementEnd(xaml, start);
        if (end < 0) end = start;

        // Read Grid.Row / Grid.Column from an attribute on this element OR from
        // an attached-property element child (rare but valid).
        // We look for attributes whose local name contains a dot — those are
        // attached properties set inline (e.g. Grid.Row="1").
        int row = 0, col = 0, rowSpan = 1, colSpan = 1;
        bool hasRow = false, hasCol = false, hasRowSpan = false, hasColSpan = false;

        foreach (var attr in element.Attributes())
        {
            switch (attr.Name.LocalName)
            {
                case "Row" when attr.Name.NamespaceName == "" && IsGridAttached(attr):
                case "Grid.Row":
                    if (int.TryParse(attr.Value, out int r)) { row = r; hasRow = true; }
                    break;
                case "Column" when IsGridAttached(attr):
                case "Grid.Column":
                    if (int.TryParse(attr.Value, out int c)) { col = c; hasCol = true; }
                    break;
                case "RowSpan" when IsGridAttached(attr):
                case "Grid.RowSpan":
                    if (int.TryParse(attr.Value, out int rs)) { rowSpan = rs; hasRowSpan = true; }
                    break;
                case "ColumnSpan" when IsGridAttached(attr):
                case "Grid.ColumnSpan":
                    if (int.TryParse(attr.Value, out int cs)) { colSpan = cs; hasColSpan = true; }
                    break;
            }
        }

        return new GridChildInfo
        {
            ElementName = element.Name.LocalName,
            Row = row,
            Column = col,
            RowSpan = rowSpan,
            ColumnSpan = colSpan,
            HasExplicitRow = hasRow,
            HasExplicitColumn = hasCol,
            HasExplicitRowSpan = hasRowSpan,
            HasExplicitColumnSpan = hasColSpan,
            StartOffset = start,
            EndOffset = end
        };
    }

    /// <summary>
    /// Determines if an attribute is a Grid attached property.
    /// XAML attached properties are written as <c>Grid.Row="…"</c> where the
    /// namespace of the attribute is typically empty (it inherits the element's
    /// default namespace) and the attribute name contains a dot.
    /// We simply check the local name includes the dot form or the owning
    /// namespace matches a known Grid namespace.
    /// </summary>
    private static bool IsGridAttached(XAttribute attr)
    {
        // In XLinq, attached properties written as Grid.Row="1" have their
        // local name set to "Grid.Row" (the whole thing) and an empty namespace.
        return attr.Name.LocalName.StartsWith("Grid.", StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Offset arithmetic helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a table mapping 1-based line numbers to 0-based character offsets
    /// of the first character on that line.
    /// </summary>
    internal static int[] BuildLineOffsetTable(string text)
    {
        var offsets = new List<int> { 0 }; // line 1 starts at offset 0
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                offsets.Add(i + 1);
        }
        return offsets.ToArray();
    }

    /// <summary>
    /// Converts a 1-based line/column pair (as reported by IXmlLineInfo) to a
    /// 0-based absolute character offset.
    /// </summary>
    internal static int LineColToOffset(int[] lineOffsets, int line, int col)
    {
        if (line < 1 || line > lineOffsets.Length) return 0;
        // IXmlLineInfo reports 1-based column, pointing to the first character
        // of the element's name (after '<').  We subtract 2 to get to '<'.
        int lineStart = lineOffsets[line - 1];
        int offset = lineStart + col - 2;
        return Math.Max(0, offset);
    }

    /// <summary>
    /// Starting from <paramref name="startOffset"/> (expected to be pointing at
    /// a <c>&lt;</c>), finds the offset just after the matching closing tag or
    /// self-closing <c>/&gt;</c>.  Returns -1 on failure.
    /// </summary>
    internal static int FindElementEnd(string xaml, int startOffset)
    {
        if (startOffset < 0 || startOffset >= xaml.Length || xaml[startOffset] != '<')
            return -1;

        try
        {
            // Use a mini XML reader over just the substring from startOffset.
            // We wrap in a root element so the fragment is valid XML.
            string fragment = xaml.Substring(startOffset);

            // Count depth using simple stack to avoid full re-parse overhead.
            int depth = 0;
            int i = 0;
            while (i < fragment.Length)
            {
                if (fragment[i] != '<') { i++; continue; }

                if (i + 1 < fragment.Length && fragment[i + 1] == '/')
                {
                    // Closing tag
                    int close = fragment.IndexOf('>', i);
                    if (close < 0) return -1;
                    depth--;
                    i = close + 1;
                    if (depth == 0) return startOffset + i;
                }
                else if (i + 1 < fragment.Length && fragment[i + 1] == '!')
                {
                    // Comment or CDATA – skip to end
                    int close = fragment.IndexOf('>', i);
                    if (close < 0) return -1;
                    i = close + 1;
                }
                else if (i + 1 < fragment.Length && fragment[i + 1] == '?')
                {
                    // Processing instruction
                    int close = fragment.IndexOf("?>", i);
                    if (close < 0) return -1;
                    i = close + 2;
                }
                else
                {
                    // Opening tag – find its '>'
                    int close = fragment.IndexOf('>', i);
                    if (close < 0) return -1;
                    bool selfClosing = close > 0 && fragment[close - 1] == '/';
                    if (!selfClosing) depth++;
                    i = close + 1;
                    if (depth == 0) return startOffset + i; // self-closing at depth 0
                }
            }
            return -1;
        }
        catch
        {
            return -1;
        }
    }
}
