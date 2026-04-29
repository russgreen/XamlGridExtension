using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace XamlGridExtension.Commands;

[Command(PackageIds.InsertRowCommandId)]
internal sealed class InsertRowCommand : GridCommandBase<InsertRowCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var doc = await GetActiveXamlDocumentAsync();
        if (doc is null) return;

        var (text, caretOffset, docView) = doc.Value;

        var gridInfo = GridService.ParseGridAtOffset(text, caretOffset);
        if (gridInfo is null) return;

        // Insert after the last row by default; the user can select a row index via tool window.
        int insertAt = gridInfo.RowCount;
        string newText = GridService.InsertRow(text, caretOffset, insertAt);

        await ReplaceDocumentTextAsync(docView, newText);
    }
}
