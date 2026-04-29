using System.Runtime.InteropServices;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace XamlGridExtension.ToolWindows;

[Guid("F1A2B3C4-D5E6-7F8A-9B0C-1D2E3F4A5B6C")]
public sealed class GridToolWindow : BaseToolWindow<GridToolWindow>
{
    public override string GetTitle(int toolWindowId) => "XAML Grid";

    public override System.Type PaneType => typeof(Pane);

    public override System.Threading.Tasks.Task<System.Windows.FrameworkElement> CreateAsync(int toolWindowId, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.FromResult<System.Windows.FrameworkElement>(new GridToolWindowControl());
    }

    [Guid("F1A2B3C4-D5E6-7F8A-9B0C-1D2E3F4A5B6C")]
    internal sealed class Pane : ToolWindowPane
    {
        public Pane() : base(null)
        {
            BitmapResourceID = 301;
            BitmapIndex = 1;
        }
    }
}
