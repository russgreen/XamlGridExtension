using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace XamlGridExtension.Commands;

[Command(PackageIds.InsertColumnCommandId)]
internal sealed class InsertColumnCommand : GridCommandBase<InsertColumnCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var doc = await GetActiveXamlDocumentAsync();
        if (doc is null) return;

        var (text, caretOffset, docView) = doc.Value;

        var gridInfo = GridService.ParseGridAtOffset(text, caretOffset);
        if (gridInfo is null) return;

        int insertAt = gridInfo.ColumnCount;
        string newText = GridService.InsertColumn(text, caretOffset, insertAt);

        await ReplaceDocumentTextAsync(docView, newText);
    }
}
