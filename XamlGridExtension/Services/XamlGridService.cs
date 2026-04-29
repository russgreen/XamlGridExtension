using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using XamlGridExtension.Models;

namespace XamlGridExtension.Services;

/// <summary>
/// Framework-agnostic XAML Grid service that works at the raw XML level.
/// Supports WPF, WinUI, and Avalonia Grid elements.
/// </summary>
public sealed class XamlGridService : IXamlGridService
{
    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public GridInfo? ParseGridAtOffset(string documentText, int caretOffset)
    {
        var gridElement = FindGridElementAtOffset(documentText, caretOffset);
        if (gridElement is null) return null;
        return BuildGridInfo(gridElement);
    }

    public string InsertRow(string documentText, int caretOffset, int rowIndex, string rowHeight = "Auto")
    {
        return ModifyDocument(documentText, caretOffset, gridElement =>
        {
            InsertRowDefinition(gridElement, rowIndex, rowHeight);
            RenumberChildren(gridElement, "Row", rowIndex, +1);
        });
    }

    public string RemoveRow(string documentText, int caretOffset, int rowIndex)
    {
        return ModifyDocument(documentText, caretOffset, gridElement =>
        {
            RemoveDefinition(gridElement, "RowDefinitions", "RowDefinition", rowIndex);
            RenumberChildren(gridElement, "Row", rowIndex, -1, remove: true);
        });
    }

    public string InsertColumn(string documentText, int caretOffset, int columnIndex, string columnWidth = "Auto")
    {
        return ModifyDocument(documentText, caretOffset, gridElement =>
        {
            InsertColumnDefinition(gridElement, columnIndex, columnWidth);
            RenumberChildren(gridElement, "Column", columnIndex, +1);
        });
    }

    public string RemoveColumn(string documentText, int caretOffset, int columnIndex)
    {
        return ModifyDocument(documentText, caretOffset, gridElement =>
        {
            RemoveDefinition(gridElement, "ColumnDefinitions", "ColumnDefinition", columnIndex);
            RenumberChildren(gridElement, "Column", columnIndex, -1, remove: true);
        });
    }

    // -------------------------------------------------------------------------
    // Grid location
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds the innermost Grid element that contains <paramref name="caretOffset"/>.
    /// Matching is done by local name "Grid" to remain framework-agnostic.
    /// </summary>
    private static XElement? FindGridElementAtOffset(string documentText, int caretOffset)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(documentText, LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            // Try to recover by wrapping in a synthetic root — useful when the document
            // contains only a fragment (e.g., a code snippet in isolation).
            try
            {
                doc = XDocument.Parse($"<_root_>{documentText}</_root_>", LoadOptions.SetLineInfo);
            }
            catch
            {
                return null;
            }
        }

        // Collect all Grid elements with their character spans.
        var grids = doc.Descendants()
            .Where(e => e.Name.LocalName == "Grid")
            .Select(e => new { Element = e, Start = GetElementStart(documentText, e), End = GetElementEnd(documentText, e) })
            .Where(g => g.Start >= 0 && caretOffset >= g.Start && caretOffset <= g.End)
            .OrderByDescending(g => g.Start) // innermost first
            .ToList();

        return grids.FirstOrDefault()?.Element;
    }

    private static int GetElementStart(string text, XElement element)
    {
        var lineInfo = (IXmlLineInfo)element;
        if (!lineInfo.HasLineInfo()) return -1;
        return LineColumnToOffset(text, lineInfo.LineNumber, lineInfo.LinePosition);
    }

    private static int GetElementEnd(string text, XElement element)
    {
        // Find the closing tag by searching after the last child / after the start tag.
        int start = GetElementStart(text, element);
        if (start < 0) return -1;

        // Walk forward to find the matching close tag depth.
        int depth = 0;
        int i = start;
        while (i < text.Length)
        {
            if (text[i] == '<')
            {
                if (i + 1 < text.Length && text[i + 1] == '/')
                {
                    if (depth == 0) return i;
                    depth--;
                }
                else if (i + 1 < text.Length && text[i + 1] != '!')
                {
                    depth++;
                }
            }
            i++;
        }
        return text.Length - 1;
    }

    private static int LineColumnToOffset(string text, int line, int column)
    {
        int currentLine = 1;
        int currentCol = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (currentLine == line && currentCol == column) return i;
            if (text[i] == '\n') { currentLine++; currentCol = 1; }
            else { currentCol++; }
        }
        return -1;
    }

    // -------------------------------------------------------------------------
    // Grid parsing
    // -------------------------------------------------------------------------

    private static GridInfo BuildGridInfo(XElement gridElement)
    {
        var info = new GridInfo();

        var rowDefs = GetDefinitions(gridElement, "RowDefinitions", "RowDefinition", "Height");
        var colDefs = GetDefinitions(gridElement, "ColumnDefinitions", "ColumnDefinition", "Width");

        info.RowHeights = rowDefs.Count > 0 ? rowDefs : new List<string> { "*" };
        info.ColumnWidths = colDefs.Count > 0 ? colDefs : new List<string> { "*" };
        info.RowCount = info.RowHeights.Count;
        info.ColumnCount = info.ColumnWidths.Count;

        foreach (var child in gridElement.Elements())
        {
            if (child.Name.LocalName is "RowDefinitions" or "ColumnDefinitions") continue;

            info.Children.Add(new GridChildInfo
            {
                ElementName = child.Name.LocalName,
                Row        = GetAttachedInt(child, "Row"),
                Column     = GetAttachedInt(child, "Column"),
                RowSpan    = Math.Max(1, GetAttachedInt(child, "RowSpan", 1)),
                ColumnSpan = Math.Max(1, GetAttachedInt(child, "ColumnSpan", 1)),
            });
        }

        return info;
    }

    private static List<string> GetDefinitions(XElement gridElement, string containerLocalName, string itemLocalName, string sizeAttribute)
    {
        var container = gridElement.Elements()
            .FirstOrDefault(e => e.Name.LocalName == containerLocalName);
        if (container is null) return new List<string>();

        return container.Elements()
            .Where(e => e.Name.LocalName == itemLocalName)
            .Select(e => e.Attribute(sizeAttribute)?.Value ?? "*")
            .ToList();
    }

    private static int GetAttachedInt(XElement element, string attachedPropertyLocalName, int defaultValue = 0)
    {
        // Attached properties can be "Grid.Row", "Grid.Column" etc. — namespace varies.
        var attr = element.Attributes()
            .FirstOrDefault(a => a.Name.LocalName == $"Grid.{attachedPropertyLocalName}");
        if (attr is null) return defaultValue;
        return int.TryParse(attr.Value, out int v) ? v : defaultValue;
    }

    // -------------------------------------------------------------------------
    // Document modification helpers
    // -------------------------------------------------------------------------

    private static string ModifyDocument(string documentText, int caretOffset, Action<XElement> modify)
    {
        XDocument doc;
        bool hasWrapper = false;
        try
        {
            doc = XDocument.Parse(documentText, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            try
            {
                doc = XDocument.Parse($"<_root_>{documentText}</_root_>", LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                hasWrapper = true;
            }
            catch
            {
                return documentText; // Cannot parse — return unchanged
            }
        }

        // Adjust offset for wrapper characters.
        int adjustedOffset = hasWrapper ? caretOffset + "<_root_>".Length : caretOffset;

        var gridElement = FindGridInDoc(doc, documentText, adjustedOffset, hasWrapper);
        if (gridElement is null) return documentText;

        modify(gridElement);

        string result = doc.ToString(SaveOptions.DisableFormatting);

        if (hasWrapper)
        {
            // Strip the synthetic wrapper tags.
            result = result.Replace("<_root_>", "").Replace("</_root_>", "");
        }

        return result;
    }

    private static XElement? FindGridInDoc(XDocument doc, string originalText, int caretOffset, bool hasWrapper)
    {
        string textForSearch = hasWrapper
            ? $"<_root_>{originalText}</_root_>"
            : originalText;

        var grids = doc.Descendants()
            .Where(e => e.Name.LocalName == "Grid")
            .Select(e => new { Element = e, Start = GetElementStart(textForSearch, e) })
            .Where(g => g.Start >= 0 && caretOffset >= g.Start)
            .OrderByDescending(g => g.Start)
            .ToList();

        return grids.FirstOrDefault()?.Element;
    }

    private static void InsertRowDefinition(XElement gridElement, int rowIndex, string height)
    {
        var container = GetOrCreateContainer(gridElement, "RowDefinitions");
        var newDef = CreateDefinition(gridElement, "RowDefinition", "Height", height);

        var rows = container.Elements().ToList();
        if (rowIndex >= rows.Count)
            container.Add(newDef);
        else
            rows[rowIndex].AddBeforeSelf(newDef);
    }

    private static void InsertColumnDefinition(XElement gridElement, int columnIndex, string width)
    {
        var container = GetOrCreateContainer(gridElement, "ColumnDefinitions");
        var newDef = CreateDefinition(gridElement, "ColumnDefinition", "Width", width);

        var cols = container.Elements().ToList();
        if (columnIndex >= cols.Count)
            container.Add(newDef);
        else
            cols[columnIndex].AddBeforeSelf(newDef);
    }

    private static void RemoveDefinition(XElement gridElement, string containerLocalName, string itemLocalName, int index)
    {
        var container = gridElement.Elements()
            .FirstOrDefault(e => e.Name.LocalName == containerLocalName);
        if (container is null) return;

        var items = container.Elements()
            .Where(e => e.Name.LocalName == itemLocalName)
            .ToList();

        if (index >= 0 && index < items.Count)
            items[index].Remove();
    }

    /// <summary>
    /// Renumber Grid.Row or Grid.Column attached properties on direct children.
    /// <paramref name="delta"/>: +1 for insert, -1 for remove.
    /// <paramref name="remove"/>: when true, children at exactly <paramref name="atIndex"/> are clamped to max-1.
    /// </summary>
    private static void RenumberChildren(XElement gridElement, string attachedName, int atIndex, int delta, bool remove = false)
    {
        foreach (var child in gridElement.Elements())
        {
            if (child.Name.LocalName is "RowDefinitions" or "ColumnDefinitions") continue;

            var attr = child.Attributes()
                .FirstOrDefault(a => a.Name.LocalName == $"Grid.{attachedName}");

            int current = 0;
            if (attr is not null && int.TryParse(attr.Value, out int parsed))
                current = parsed;

            int updated = current;
            if (delta > 0 && current >= atIndex)
                updated = current + delta;
            else if (delta < 0 && current > atIndex)
                updated = current + delta; // delta is negative
            else if (delta < 0 && remove && current == atIndex)
                updated = Math.Max(0, atIndex - 1);

            if (updated != current)
                SetOrCreateAttachedAttribute(child, $"Grid.{attachedName}", updated.ToString(), gridElement);
        }
    }

    private static void SetOrCreateAttachedAttribute(XElement child, string localName, string value, XElement gridElement)
    {
        // Try to preserve the existing namespace prefix used in the document.
        var existing = child.Attributes().FirstOrDefault(a => a.Name.LocalName == localName);
        if (existing is not null)
        {
            existing.Value = value;
            return;
        }

        // Determine namespace from existing Grid.Row/Grid.Column attrs on other children, or use no namespace.
        XNamespace ns = XNamespace.None;
        var sample = gridElement.Descendants()
            .SelectMany(e => e.Attributes())
            .FirstOrDefault(a => a.Name.LocalName.StartsWith("Grid."));
        if (sample is not null) ns = sample.Name.Namespace;

        child.SetAttributeValue(ns + localName, value);
    }

    private static XElement GetOrCreateContainer(XElement gridElement, string containerLocalName)
    {
        var existing = gridElement.Elements()
            .FirstOrDefault(e => e.Name.LocalName == containerLocalName);
        if (existing is not null) return existing;

        // Create under the same namespace as the Grid element.
        var newContainer = new XElement(gridElement.Name.Namespace + containerLocalName);
        // Insert before first non-definition child.
        var firstChild = gridElement.Elements()
            .FirstOrDefault(e => e.Name.LocalName is not "RowDefinitions" and not "ColumnDefinitions");
        if (firstChild is not null)
            firstChild.AddBeforeSelf(newContainer);
        else
            gridElement.Add(newContainer);
        return newContainer;
    }

    private static XElement CreateDefinition(XElement gridElement, string localName, string sizeAttr, string sizeValue)
    {
        return new XElement(gridElement.Name.Namespace + localName,
            new XAttribute(sizeAttr, sizeValue));
    }
}
