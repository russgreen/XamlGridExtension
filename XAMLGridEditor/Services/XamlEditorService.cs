using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using XAMLGridEditor.Core;

namespace XAMLGridEditor.Services;

/// <summary>
/// Provides access to the active XAML document and applies Grid manipulations
/// to it using a VS text-edit transaction (so changes are undoable).
/// </summary>
internal sealed class XamlEditorService
{
    private readonly AsyncPackage _package;

    public XamlEditorService(AsyncPackage package)
    {
        _package = package;
    }

    // -----------------------------------------------------------------------
    // Public API called by commands and tool window
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the GridInfo for the grid containing the current caret position,
    /// or <c>null</c> when the caret is not inside a grid.
    /// Must be called on the UI thread.
    /// </summary>
    public GridInfo? GetCurrentGrid()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var (xaml, caretOffset) = GetXamlAndCaret();
        if (xaml is null) return null;
        return XamlGridParser.FindGridAtOffset(xaml, caretOffset);
    }

    public void InsertRow(GridInfo grid, string height = "*")
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ApplyManipulation((xaml) => GridManipulator.InsertRow(xaml, grid, grid.RowCount, height));
    }

    public void InsertRowBefore(GridInfo grid, int beforeIndex, string height = "*")
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ApplyManipulation((xaml) => GridManipulator.InsertRow(xaml, grid, beforeIndex, height));
    }

    public void InsertColumn(GridInfo grid, string width = "*")
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ApplyManipulation((xaml) => GridManipulator.InsertColumn(xaml, grid, grid.ColumnCount, width));
    }

    public void InsertColumnBefore(GridInfo grid, int beforeIndex, string width = "*")
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ApplyManipulation((xaml) => GridManipulator.InsertColumn(xaml, grid, beforeIndex, width));
    }

    public void RemoveRow(GridInfo grid, int rowIndex)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ApplyManipulationWithWarnings(rowIndex, isRow: true);
    }

    public void RemoveColumn(GridInfo grid, int colIndex)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ApplyManipulationWithWarnings(colIndex, isRow: false);
    }

    public void SetRowSize(GridInfo grid, int rowIndex, string newHeight)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ApplyManipulation(xaml => GridManipulator.SetRowSize(xaml, grid, rowIndex, newHeight));
    }

    public void SetColumnSize(GridInfo grid, int colIndex, string newWidth)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ApplyManipulation(xaml => GridManipulator.SetColumnSize(xaml, grid, colIndex, newWidth));
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void ApplyManipulation(Func<string, string> manipulate)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var wpfTextView = GetActiveWpfTextView();
        var textBuffer = wpfTextView?.TextBuffer;
        if (textBuffer is null) return;

        string original = textBuffer.CurrentSnapshot.GetText();
        string updated = manipulate(original);
        if (updated == original) return;

        ApplyBufferUpdatePreservingViewport(wpfTextView!, textBuffer, original, updated);
    }

    private void ApplyManipulationWithWarnings(int index, bool isRow)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var wpfTextView = GetActiveWpfTextView();
        var textBuffer = wpfTextView?.TextBuffer;
        if (textBuffer is null) return;

        string original = textBuffer.CurrentSnapshot.GetText();
        int caretOffset = GetCaretOffset() ?? 0;

        var grid = XamlGridParser.FindGridAtOffset(original, caretOffset);
        if (grid is null) return;

        System.Collections.Generic.List<string> warnings;
        string updated = isRow
            ? GridManipulator.RemoveRow(original, grid, index, out warnings)
            : GridManipulator.RemoveColumn(original, grid, index, out warnings);

        if (warnings.Count > 0)
        {
            string msg = string.Join(Environment.NewLine, warnings);
            VsShellUtilities.ShowMessageBox(
                _package,
                msg,
                "XAMLGridEditor – Warning",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        if (updated == original) return;

        ApplyBufferUpdatePreservingViewport(wpfTextView!, textBuffer, original, updated);
    }

    private static void ApplyBufferUpdatePreservingViewport(
        IWpfTextView wpfTextView,
        ITextBuffer textBuffer,
        string original,
        string updated)
    {
        int caretPosition = wpfTextView.Caret.Position.BufferPosition.Position;

        using var edit = textBuffer.CreateEdit();
        edit.Replace(new Span(0, original.Length), updated);
        edit.Apply();

        var newSnapshot = textBuffer.CurrentSnapshot;
        int newCaretPosition = Math.Min(caretPosition, newSnapshot.Length);
        var newCaretPoint = new SnapshotPoint(newSnapshot, newCaretPosition);
        wpfTextView.Caret.MoveTo(newCaretPoint);
        wpfTextView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(newCaretPoint, 0));
    }

    /// <summary>
    /// Returns focus to the active XAML editor after UI updates have settled.
    /// Call this after <see cref="Refresh"/> to avoid the tool window stealing focus back.
    /// </summary>
    public void FocusEditor()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var sp = (System.IServiceProvider)_package;
        var monSel = sp.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
        if (monSel is null) return;

        monSel.GetCurrentElementValue(
            (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame,
            out object frameObj);

        if (frameObj is IVsWindowFrame frame)
            frame.Show();

        // frame.Show() activates the tab but doesn't move keyboard focus into
        // the editor pane. Defer Focus() so it fires after VS finishes activating
        // the frame, landing the caret back at its previous position.
        var wpfTextView = GetActiveWpfTextView();
        if (wpfTextView is null) return;

#pragma warning disable VSTHRD001 // BeginInvoke with priority is intentional here
#pragma warning disable VSTHRD110 // Fire-and-forget focus restore
        wpfTextView.VisualElement.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            new Action(() => wpfTextView.VisualElement.Focus()));
#pragma warning restore VSTHRD110
#pragma warning restore VSTHRD001
    }

    private IWpfTextView? GetActiveWpfTextView()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var sp = (System.IServiceProvider)_package;
        var textManager = sp.GetService(typeof(SVsTextManager)) as IVsTextManager;
        if (textManager is null) return null;

        // fMustHaveFocus=0: return the most-recently-active text view even if
        // focus is currently on another element (e.g. the tool window itself).
        textManager.GetActiveView(0, null, out var vsTextView);
        if (vsTextView is null) return null;

        var componentModel = sp.GetService(typeof(SComponentModel)) as IComponentModel;
        if (componentModel is null) return null;

        var adapterFactory = componentModel.DefaultExportProvider
            .GetExportedValueOrDefault<IVsEditorAdaptersFactoryService>();
        return adapterFactory?.GetWpfTextView(vsTextView);
    }

    /// <summary>
    /// Returns the currently active <see cref="IWpfTextView"/>, or <c>null</c>.
    /// Must be called on the UI thread.
    /// </summary>
    internal IWpfTextView? GetActiveView()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return GetActiveWpfTextView();
    }

    /// <summary>
    /// Returns the row or column index to insert before, based on the current caret position:
    /// <list type="bullet">
    ///   <item>Caret on a <c>RowDefinition</c> / <c>ColumnDefinition</c> → that definition's index.</item>
    ///   <item>Caret on a child control with <c>Grid.Row</c> / <c>Grid.Column</c> → that child's row/column value.</item>
    ///   <item>Otherwise → -1 (caller should fall back to a default).</item>
    /// </list>
    /// </summary>
    public int GetDefinitionIndexAtCaret(GridInfo grid, bool isRow)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        int? caret = GetCaretOffset();
        if (caret is null) return -1;

        // 1. Caret is directly on a definition element
        var definitions = isRow ? grid.Rows : grid.Columns;
        for (int i = 0; i < definitions.Count; i++)
        {
            var def = definitions[i];
            if (caret >= def.StartOffset && caret <= def.EndOffset)
                return i;
        }

        // 2. Caret is on a child control — read its Grid.Row / Grid.Column
        foreach (var child in grid.Children)
        {
            if (caret >= child.StartOffset && caret <= child.EndOffset)
                return isRow ? child.Row : child.Column;
        }

        return -1;
    }

    private (string? Xaml, int CaretOffset) GetXamlAndCaret()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var wpfTextView = GetActiveWpfTextView();
        if (wpfTextView is null) return (null, 0);

        string xaml = wpfTextView.TextBuffer.CurrentSnapshot.GetText();
        int caretOffset = wpfTextView.Caret.Position.BufferPosition.Position;
        return (xaml, caretOffset);
    }

    private int? GetCaretOffset()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var (_, offset) = GetXamlAndCaret();
        return offset;
    }
}
