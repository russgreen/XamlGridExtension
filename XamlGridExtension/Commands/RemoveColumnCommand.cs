using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace XamlGridExtension.Commands;

[Command(PackageIds.RemoveColumnCommandId)]
internal sealed class RemoveColumnCommand : GridCommandBase<RemoveColumnCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var doc = await GetActiveXamlDocumentAsync();
        if (doc is null) return;

        var (text, caretOffset, docView) = doc.Value;

        var gridInfo = GridService.ParseGridAtOffset(text, caretOffset);
        if (gridInfo is null || gridInfo.ColumnCount <= 1) return;

        int removeAt = gridInfo.ColumnCount - 1;
        string newText = GridService.RemoveColumn(text, caretOffset, removeAt);

        await ReplaceDocumentTextAsync(docView, newText);
    }
}
