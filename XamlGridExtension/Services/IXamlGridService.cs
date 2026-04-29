using XamlGridExtension.Models;

namespace XamlGridExtension.Services;

public interface IXamlGridService
{
    /// <summary>
    /// Parse the Grid element that contains the given character offset in the document text.
    /// Returns null if the caret is not inside a Grid element.
    /// </summary>
    GridInfo? ParseGridAtOffset(string documentText, int caretOffset);

    /// <summary>Insert an empty row at <paramref name="rowIndex"/> and renumber child Grid.Row attributes.</summary>
    string InsertRow(string documentText, int caretOffset, int rowIndex, string rowHeight = "Auto");

    /// <summary>Remove the row at <paramref name="rowIndex"/> and renumber child Grid.Row attributes.</summary>
    string RemoveRow(string documentText, int caretOffset, int rowIndex);

    /// <summary>Insert an empty column at <paramref name="columnIndex"/> and renumber child Grid.Column attributes.</summary>
    string InsertColumn(string documentText, int caretOffset, int columnIndex, string columnWidth = "Auto");

    /// <summary>Remove the column at <paramref name="columnIndex"/> and renumber child Grid.Column attributes.</summary>
    string RemoveColumn(string documentText, int caretOffset, int columnIndex);
}
