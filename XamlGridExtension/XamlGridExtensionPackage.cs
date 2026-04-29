using System;
using System.Runtime.InteropServices;
using System.Threading;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace XamlGridExtension;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("#110", "#112", "1.0.0", IconResourceID = 400)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(ToolWindows.GridToolWindow))]
[Guid(PackageGuids.XamlGridExtensionPackageString)]
public sealed class XamlGridExtensionPackage : ToolkitPackage
{
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        await Commands.InsertRowCommand.InitializeAsync(this);
        await Commands.RemoveRowCommand.InitializeAsync(this);
        await Commands.InsertColumnCommand.InitializeAsync(this);
        await Commands.RemoveColumnCommand.InitializeAsync(this);
    }
}
