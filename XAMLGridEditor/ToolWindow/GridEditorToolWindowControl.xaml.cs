using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XAMLGridEditor.ToolWindow;

/// <summary>
/// Code-behind for <see cref="GridEditorToolWindowControl"/>.
/// The ViewModel is set by <see cref="GridEditorToolWindow"/> after
/// the editor service is available.
/// </summary>
public partial class GridEditorToolWindowControl : UserControl
{
    public GridEditorToolWindowControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Commits the size edit when the TextBox loses focus.
    /// </summary>
    private void SizeTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is DefinitionItem item)
        {
            // Force the binding to push before we commit.
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            item.CommitSize();
        }
    }

    /// <summary>
    /// Commits the size edit when the user presses Enter, and moves focus away.
    /// </summary>
    private void SizeTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb && tb.DataContext is DefinitionItem item)
        {
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            item.CommitSize();
            // Move focus to the parent so the TextBox cleanly loses focus.
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }
}
