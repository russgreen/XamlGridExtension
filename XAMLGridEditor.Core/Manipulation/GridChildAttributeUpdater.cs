using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XAMLGridEditor.Core;

/// <summary>
/// Updates Grid.Row, Grid.Column, Grid.RowSpan and Grid.ColumnSpan attributes
/// on child elements of a Grid when rows or columns are inserted or removed.
/// </summary>
public static class GridChildAttributeUpdater
{
    // -----------------------------------------------------------------------
    // Insert
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adjusts child element attributes after a row is inserted before
    /// <paramref name="beforeIndex"/>.
    /// </summary>
    /// <param name="xaml">Full XAML document text.</param>
    /// <param name="children">Children parsed from the target grid.</param>
    /// <param name="beforeIndex">0-based row index that was inserted before.</param>
    /// <returns>Updated XAML text.</returns>
    public static string InsertRowAttributes(string xaml, IList<GridChildInfo> children, int beforeIndex)
        => AdjustAttributes(xaml, children, isRow: true, isInsert: true, targetIndex: beforeIndex);

    /// <summary>
    /// Adjusts child element attributes after a column is inserted before
    /// <paramref name="beforeIndex"/>.
    /// </summary>
    public static string InsertColumnAttributes(string xaml, IList<GridChildInfo> children, int beforeIndex)
        => AdjustAttributes(xaml, children, isRow: false, isInsert: true, targetIndex: beforeIndex);

    // -----------------------------------------------------------------------
    // Remove
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adjusts child element attributes after a row at <paramref name="removeIndex"/>
    /// is removed.
    /// </summary>
    /// <param name="xaml">Full XAML document text.</param>
    /// <param name="children">Children parsed from the target grid.</param>
    /// <param name="removeIndex">0-based row index that was removed.</param>
    /// <param name="warnings">Populated with warning messages for elements on the removed row.</param>
    /// <returns>Updated XAML text.</returns>
    public static string RemoveRowAttributes(string xaml, IList<GridChildInfo> children,
        int removeIndex, out List<string> warnings)
        => AdjustAttributes(xaml, children, isRow: true, isInsert: false,
            targetIndex: removeIndex, out warnings);

    /// <summary>
    /// Adjusts child element attributes after a column at <paramref name="removeIndex"/>
    /// is removed.
    /// </summary>
    public static string RemoveColumnAttributes(string xaml, IList<GridChildInfo> children,
        int removeIndex, out List<string> warnings)
        => AdjustAttributes(xaml, children, isRow: false, isInsert: false,
            targetIndex: removeIndex, out warnings);

    // -----------------------------------------------------------------------
    // Core adjustment logic
    // -----------------------------------------------------------------------

    private static string AdjustAttributes(string xaml, IList<GridChildInfo> children,
        bool isRow, bool isInsert, int targetIndex)
    {
        List<string> _ = new();
        return AdjustAttributes(xaml, children, isRow, isInsert, targetIndex, out _);
    }

    private static string AdjustAttributes(string xaml, IList<GridChildInfo> children,
        bool isRow, bool isInsert, int targetIndex, out List<string> warnings)
    {
        warnings = new List<string>();

        // Collect all text replacements as (startOffset, endOffset, newText) tuples.
        // We accumulate them in reverse document order so that applying them doesn't
        // shift the offsets of earlier replacements.
        var replacements = new List<(int Start, int End, string New)>();

        foreach (var child in children)
        {
            int position = isRow ? child.Row : child.Column;
            int span = isRow ? child.RowSpan : child.ColumnSpan;
            bool hasExplicitPos = isRow ? child.HasExplicitRow : child.HasExplicitColumn;
            bool hasExplicitSpan = isRow ? child.HasExplicitRowSpan : child.HasExplicitColumnSpan;

            string posAttr = isRow ? "Grid.Row" : "Grid.Column";
            string spanAttr = isRow ? "Grid.RowSpan" : "Grid.ColumnSpan";

            if (isInsert)
            {
                // --- INSERT ---
                if (position >= targetIndex)
                {
                    // This element's position moves up by 1.
                    int newPos = position + 1;
                    if (hasExplicitPos)
                    {
                        var (s, e) = FindAttributeValueSpan(xaml, child.StartOffset, posAttr);
                        if (s >= 0)
                            replacements.Add((s, e, newPos.ToString()));
                    }
                    else
                    {
                        // Implicit 0 that now needs to become 1 – add the attribute.
                        replacements.Add(InsertAttributeAfterTagName(xaml, child.StartOffset, posAttr, newPos.ToString()));
                    }
                }

                // If span straddles the insertion point, grow span by 1.
                // Straddle: element starts before targetIndex AND ends at or after targetIndex.
                // i.e.  position < targetIndex  AND  position + span > targetIndex
                if (position < targetIndex && position + span > targetIndex)
                {
                    int newSpan = span + 1;
                    if (hasExplicitSpan)
                    {
                        var (s, e) = FindAttributeValueSpan(xaml, child.StartOffset, spanAttr);
                        if (s >= 0)
                            replacements.Add((s, e, newSpan.ToString()));
                    }
                    else
                    {
                        replacements.Add(InsertAttributeAfterTagName(xaml, child.StartOffset, spanAttr, newSpan.ToString()));
                    }
                }
            }
            else
            {
                // --- REMOVE ---
                if (position == targetIndex)
                {
                    warnings.Add(
                        $"Element '{child.ElementName}' at {posAttr}={position} is on the removed definition and will move to {posAttr}=0.");
                    // Element lands on row/col 0 after removal; keep existing or set to 0.
                    if (hasExplicitPos)
                    {
                        var (s, e) = FindAttributeValueSpan(xaml, child.StartOffset, posAttr);
                        if (s >= 0)
                            replacements.Add((s, e, "0"));
                    }
                }
                else if (position > targetIndex)
                {
                    int newPos = position - 1;
                    if (hasExplicitPos)
                    {
                        var (s, e) = FindAttributeValueSpan(xaml, child.StartOffset, posAttr);
                        if (s >= 0)
                            replacements.Add((s, e, newPos.ToString()));
                    }
                    // If implicit 0, it can't be > targetIndex (targetIndex >= 1), so no action needed.
                }

                // Clamp span if it covers the removed index.
                // Element covers targetIndex when: position <= targetIndex < position + span
                if (position <= targetIndex && targetIndex < position + span && span > 1)
                {
                    int newSpan = span - 1;
                    if (newSpan < 1) newSpan = 1;
                    if (hasExplicitSpan)
                    {
                        var (s, e) = FindAttributeValueSpan(xaml, child.StartOffset, spanAttr);
                        if (s >= 0)
                            replacements.Add((s, e, newSpan.ToString()));
                    }
                    if (span > 1)
                    {
                        warnings.Add(
                            $"Element '{child.ElementName}' {spanAttr} reduced from {span} to {newSpan} due to row/column removal.");
                    }
                }
            }
        }

        // Apply replacements in reverse order so offsets remain valid.
        replacements.Sort((a, b) => b.Start.CompareTo(a.Start));
        var result = xaml;
        foreach (var (s, e, newText) in replacements)
        {
            result = result.Substring(0, s) + newText + result.Substring(e);
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Attribute location helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds the start and end offset of the *value* of an attribute on the
    /// element that starts at <paramref name="elementOffset"/>.
    /// Returns (-1, -1) when not found.
    /// </summary>
    public static (int Start, int End) FindAttributeValueSpan(string xaml, int elementOffset, string attributeName)
    {
        // Locate the opening tag region (up to the first unquoted '>').
        int tagEnd = FindTagEnd(xaml, elementOffset);
        if (tagEnd < 0) return (-1, -1);

        string tagText = xaml.Substring(elementOffset, tagEnd - elementOffset);

        // Build a regex that matches  attributeName="value"  or  attributeName='value'
        // The attribute name may be preceded by whitespace.
        string escaped = Regex.Escape(attributeName);
        var regex = new Regex($@"(?<=[<\s]){escaped}\s*=\s*(?<q>[""'])(?<val>[^""']*)(\k<q>)", RegexOptions.None);

        var match = regex.Match(tagText);
        if (!match.Success) return (-1, -1);

        var valGroup = match.Groups["val"];
        int absoluteStart = elementOffset + valGroup.Index;
        int absoluteEnd = absoluteStart + valGroup.Length;
        return (absoluteStart, absoluteEnd);
    }

    /// <summary>
    /// Finds the offset just past the <c>&gt;</c> (or <c>/&gt;</c>) that closes the
    /// opening tag starting at <paramref name="elementOffset"/>.  Returns -1 on failure.
    /// </summary>
    private static int FindTagEnd(string xaml, int elementOffset)
    {
        bool inQuote = false;
        char quoteChar = '"';
        for (int i = elementOffset; i < xaml.Length; i++)
        {
            char c = xaml[i];
            if (inQuote)
            {
                if (c == quoteChar) inQuote = false;
            }
            else
            {
                if (c == '"' || c == '\'') { inQuote = true; quoteChar = c; }
                else if (c == '>') return i + 1;
            }
        }
        return -1;
    }

    /// <summary>
    /// Builds a replacement tuple that inserts a new attribute after the tag name
    /// on the element starting at <paramref name="elementOffset"/>.
    /// The "replacement" is a zero-length insertion (Start == End).
    /// </summary>
    private static (int Start, int End, string New) InsertAttributeAfterTagName(
        string xaml, int elementOffset, string attrName, string attrValue)
    {
        // Skip '<' then the tag name (letters, digits, dots, colons, underscores, hyphens)
        int i = elementOffset + 1; // skip '<'
        while (i < xaml.Length && (char.IsLetterOrDigit(xaml[i]) || xaml[i] is '.' or ':' or '_' or '-'))
            i++;

        // Insert point is right after the tag name.
        return (i, i, $" {attrName}=\"{attrValue}\"");
    }
}
