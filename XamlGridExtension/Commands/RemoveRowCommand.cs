using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace XamlGridExtension.Commands;

[Command(PackageIds.RemoveRowCommandId)]
internal sealed class RemoveRowCommand : GridCommandBase<RemoveRowCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var doc = await GetActiveXamlDocumentAsync();
        if (doc is null) return;

        var (text, caretOffset, docView) = doc.Value;

        var gridInfo = GridService.ParseGridAtOffset(text, caretOffset);
        if (gridInfo is null || gridInfo.RowCount <= 1) return;

        // Remove the last row by default.
        int removeAt = gridInfo.RowCount - 1;
        string newText = GridService.RemoveRow(text, caretOffset, removeAt);

        await ReplaceDocumentTextAsync(docView, newText);
    }
}
