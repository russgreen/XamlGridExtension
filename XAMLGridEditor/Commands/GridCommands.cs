using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using XAMLGridEditor.Core;
using XAMLGridEditor.Services;
using Task = System.Threading.Tasks.Task;

namespace XAMLGridEditor.Commands;

/// <summary>
/// Shared GUIDs and IDs for the XAMLGridEditor command set.
/// These must match the values in GridEditorCommands.vsct.
/// </summary>
internal static class GridEditorCommandIds
{
    public static readonly Guid CommandSet = new("c1a2b3c4-d5e6-7f8a-9b0c-d1e2f3a4b5c6");

    public const int InsertRowBefore    = 0x0100;
    public const int InsertColumnBefore = 0x0101;
    public const int RemoveRow          = 0x0102;
    public const int RemoveColumn       = 0x0103;
    public const int OpenToolWindow     = 0x0104;
}

/// <summary>
/// Base class for all XAMLGridEditor commands.
/// Handles <c>BeforeQueryStatus</c> (enabled only when caret is inside a Grid)
/// and provides a hook for subclasses to implement execution.
/// </summary>
internal abstract class GridCommandBase
{
    protected readonly AsyncPackage Package;
    protected readonly XamlEditorService EditorService;

    protected GridCommandBase(AsyncPackage package, XamlEditorService editorService,
        int commandId)
    {
        Package = package;
        EditorService = editorService;

        var commandService = ((System.IServiceProvider)package).GetService(typeof(IMenuCommandService)) as IMenuCommandService;
        if (commandService is null) return;

        var id = new CommandID(GridEditorCommandIds.CommandSet, commandId);
        var cmd = new OleMenuCommand(Execute, id);
        cmd.BeforeQueryStatus += OnBeforeQueryStatus;
        commandService.AddCommand(cmd);
    }

    private void OnBeforeQueryStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not OleMenuCommand cmd) return;
        try
        {
            var grid = EditorService.GetCurrentGrid();
            cmd.Enabled = cmd.Visible = grid != null;
        }
        catch
        {
            cmd.Enabled = cmd.Visible = false;
        }
    }

    protected abstract void Execute(object sender, EventArgs e);
}

// ---------------------------------------------------------------------------
// Concrete commands
// ---------------------------------------------------------------------------

internal sealed class InsertRowBeforeCommand : GridCommandBase
{
    public InsertRowBeforeCommand(AsyncPackage package, XamlEditorService editorService)
        : base(package, editorService, GridEditorCommandIds.InsertRowBefore) { }

    protected override void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = EditorService.GetCurrentGrid();
        if (grid is null) return;
        // Insert before the row that the caret is logically in (row 0 by default).
        EditorService.InsertRowBefore(grid, 0);
    }
}

internal sealed class InsertColumnBeforeCommand : GridCommandBase
{
    public InsertColumnBeforeCommand(AsyncPackage package, XamlEditorService editorService)
        : base(package, editorService, GridEditorCommandIds.InsertColumnBefore) { }

    protected override void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = EditorService.GetCurrentGrid();
        if (grid is null) return;
        EditorService.InsertColumnBefore(grid, 0);
    }
}

internal sealed class RemoveRowCommand : GridCommandBase
{
    public RemoveRowCommand(AsyncPackage package, XamlEditorService editorService)
        : base(package, editorService, GridEditorCommandIds.RemoveRow) { }

    protected override void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = EditorService.GetCurrentGrid();
        if (grid is null) return;
        // Remove row 0 as a default; the Tool Window provides index-specific removal.
        EditorService.RemoveRow(grid, 0);
    }
}

internal sealed class RemoveColumnCommand : GridCommandBase
{
    public RemoveColumnCommand(AsyncPackage package, XamlEditorService editorService)
        : base(package, editorService, GridEditorCommandIds.RemoveColumn) { }

    protected override void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = EditorService.GetCurrentGrid();
        if (grid is null) return;
        EditorService.RemoveColumn(grid, 0);
    }
}
