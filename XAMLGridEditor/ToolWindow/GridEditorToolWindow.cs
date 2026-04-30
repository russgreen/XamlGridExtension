using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using XAMLGridEditor.Services;

namespace XAMLGridEditor.ToolWindow;

/// <summary>
/// Visual Studio Tool Window that hosts the XAMLGridEditor control.
/// </summary>
[Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
public sealed class GridEditorToolWindow : ToolWindowPane
{
    private readonly GridEditorToolWindowControl _control;

    public GridEditorToolWindow() : base(null)
    {
        Caption = "XAML Grid Editor";
        _control = new GridEditorToolWindowControl();
        Content = _control;
    }

    /// <summary>
    /// Called by VS when the tool window pane is created (including when VS
    /// restores it from a previous session after the package has already loaded).
    /// </summary>
    public override void OnToolWindowCreated()
    {
        base.OnToolWindowCreated();
        // Package may already be initialized (window created after InitializeAsync).
        if (XAMLGridEditorPackage.Instance is { } pkg && pkg.EditorService is { } svc)
            Initialize(pkg, svc);
    }

    /// <summary>
    /// Initializes the ViewModel with the editor service. Safe to call multiple times.
    /// </summary>
    internal void Initialize(AsyncPackage package, XamlEditorService editorService)
    {
        if (_control.DataContext is GridEditorToolWindowViewModel existing)
            existing.Dispose();

        _control.DataContext = new GridEditorToolWindowViewModel(package, editorService);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _control.DataContext is GridEditorToolWindowViewModel vm)
            vm.Dispose();
        base.Dispose(disposing);
    }
}
