using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using XAMLGridEditor.Commands;
using XAMLGridEditor.Services;
using XAMLGridEditor.ToolWindow;

namespace XAMLGridEditor;

/// <summary>
/// This is the class that implements the package exposed by this assembly.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(XAMLGridEditorPackage.PackageGuidString)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(GridEditorToolWindow))]
[ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string,
    PackageAutoLoadFlags.BackgroundLoad)]
public sealed class XAMLGridEditorPackage : AsyncPackage
{
    /// <summary>
    /// XAMLGridEditorPackage GUID string.
    /// </summary>
    public const string PackageGuidString = "ebcb8c35-c183-43bf-92e8-b0264e7410a8";

    /// <summary>Singleton set once <see cref="InitializeAsync"/> completes.</summary>
    internal static XAMLGridEditorPackage? Instance { get; private set; }

    private XamlEditorService? _editorService;
    internal XamlEditorService? EditorService => _editorService;

    #region Package Members

    /// <summary>
    /// Initialization of the package; this method is called right after the package is sited, so this is the place
    /// where you can put all the initialization code that rely on services provided by VisualStudio.
    /// </summary>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        _editorService = new XamlEditorService(this);
        Instance = this;

        // If the tool window was already open (restored from a previous VS session),
        // initialize its ViewModel now — OpenToolWindow won't be called in that case.
        if (FindToolWindow(typeof(GridEditorToolWindow), 0, false) is GridEditorToolWindow existingWindow)
            existingWindow.Initialize(this, _editorService);

        // Register context-menu commands.
        _ = new InsertRowBeforeCommand(this, _editorService);
        _ = new InsertColumnBeforeCommand(this, _editorService);
        _ = new RemoveRowCommand(this, _editorService);
        _ = new RemoveColumnCommand(this, _editorService);

        // Register the "open tool window" command.
        var openCmd = new Microsoft.VisualStudio.Shell.OleMenuCommand(OpenToolWindow,
            new System.ComponentModel.Design.CommandID(GridEditorCommandIds.CommandSet, GridEditorCommandIds.OpenToolWindow));
        var commandService = (IMenuCommandService?)GetService(typeof(IMenuCommandService));
        commandService?.AddCommand(openCmd);
    }

    private void OpenToolWindow(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var window = FindToolWindow(typeof(GridEditorToolWindow), 0, true) as GridEditorToolWindow;
        if (window is null) return;
        window.Initialize(this, _editorService!);
        if (window.Frame is Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame frame)
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
    }

    #endregion
}
