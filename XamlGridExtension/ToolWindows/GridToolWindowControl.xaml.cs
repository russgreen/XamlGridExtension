using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using XamlGridExtension.Models;
using XamlGridExtension.Services;

namespace XamlGridExtension.ToolWindows;

public partial class GridToolWindowControl : UserControl
{
    private static readonly IXamlGridService _gridService = new XamlGridService();
    private CancellationTokenSource? _debounceCts;
    private GridInfo? _currentGrid;
    private IWpfTextView? _subscribedView;

    public GridToolWindowControl()
    {
        InitializeComponent();

        // Hook document open/close to subscribe caret events on the new view.
        VS.Events.DocumentEvents.Opened += _ => SubscribeToActiveViewAsync().FireAndForget();
        VS.Events.DocumentEvents.Saved  += _ => RefreshWithDebounceAsync().FireAndForget();

        _ = SubscribeToActiveViewAsync();
    }

    // -------------------------------------------------------------------------
    // Caret subscription
    // -------------------------------------------------------------------------

    private async Task SubscribeToActiveViewAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var docView = await VS.Documents.GetActiveDocumentViewAsync();
        var newView = docView?.TextView as IWpfTextView;

        if (newView == _subscribedView) return;

        if (_subscribedView is not null)
            _subscribedView.Caret.PositionChanged -= OnCaretPositionChanged;

        _subscribedView = newView;

        if (_subscribedView is not null)
            _subscribedView.Caret.PositionChanged += OnCaretPositionChanged;

        await RefreshWithDebounceAsync();
    }

    private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
    {
        _ = RefreshWithDebounceAsync();
    }

    // -------------------------------------------------------------------------
    // Debounced refresh
    // -------------------------------------------------------------------------

    private async Task RefreshWithDebounceAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;
            await RefreshGridDisplayAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async Task RefreshGridDisplayAsync()
    {
        var docView = await VS.Documents.GetActiveDocumentViewAsync();
        if (docView?.TextView is null)
        {
            await UpdateUiAsync(null);
            return;
        }

        var snapshot  = docView.TextView.TextSnapshot;
        var caretPos  = docView.TextView.Caret.Position.BufferPosition.Position;
        var text      = snapshot.GetText();
        var gridInfo  = _gridService.ParseGridAtOffset(text, caretPos);
        await UpdateUiAsync(gridInfo);
    }

    // -------------------------------------------------------------------------
    // UI update
    // -------------------------------------------------------------------------

    private async Task UpdateUiAsync(GridInfo? gridInfo)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        _currentGrid = gridInfo;

        if (gridInfo is null)
        {
            GridInfoText.Text = "No Grid at caret";
            GridVisual.ItemsSource = null;
            SetButtonsEnabled(false);
            return;
        }

        GridInfoText.Text = $"{gridInfo.RowCount} row(s) × {gridInfo.ColumnCount} col(s)  |  {gridInfo.Children.Count} child(ren)";
        SetButtonsEnabled(true);
        RenderGridVisual(gridInfo);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        InsertRowBtn.IsEnabled = enabled;
        InsertColBtn.IsEnabled = enabled;
        RemoveRowBtn.IsEnabled = enabled && (_currentGrid?.RowCount ?? 0) > 1;
        RemoveColBtn.IsEnabled = enabled && (_currentGrid?.ColumnCount ?? 0) > 1;
    }

    private void RenderGridVisual(GridInfo gridInfo)
    {
        const double CellWidth  = 60;
        const double CellHeight = 30;
        const double Margin     = 1;

        var canvas = new Canvas
        {
            Width  = gridInfo.ColumnCount * (CellWidth  + Margin) + Margin,
            Height = gridInfo.RowCount    * (CellHeight + Margin) + Margin,
        };

        for (int r = 0; r < gridInfo.RowCount; r++)
        {
            for (int c = 0; c < gridInfo.ColumnCount; c++)
            {
                var cell = new Border
                {
                    Width           = CellWidth,
                    Height          = CellHeight,
                    BorderBrush     = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Background      = Brushes.Transparent,
                };
                Canvas.SetLeft(cell, Margin + c * (CellWidth  + Margin));
                Canvas.SetTop(cell,  Margin + r * (CellHeight + Margin));
                canvas.Children.Add(cell);
            }
        }

        foreach (var child in gridInfo.Children)
        {
            int row     = Math.Min(child.Row,        gridInfo.RowCount    - 1);
            int col     = Math.Min(child.Column,     gridInfo.ColumnCount - 1);
            int rowSpan = Math.Min(child.RowSpan,    gridInfo.RowCount    - row);
            int colSpan = Math.Min(child.ColumnSpan, gridInfo.ColumnCount - col);

            var label = new Border
            {
                Width      = colSpan * (CellWidth  + Margin) - Margin * 2,
                Height     = rowSpan * (CellHeight + Margin) - Margin * 2,
                Background = new SolidColorBrush(Color.FromArgb(100, 70, 130, 180)),
                Child      = new TextBlock
                {
                    Text                = child.ElementName,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    FontSize            = 10,
                    TextTrimming        = TextTrimming.CharacterEllipsis,
                    Margin              = new Thickness(2),
                }
            };
            Canvas.SetLeft(label, Margin * 2 + col * (CellWidth  + Margin));
            Canvas.SetTop(label,  Margin * 2 + row * (CellHeight + Margin));
            canvas.Children.Add(label);
        }

        GridVisual.ItemsSource = new[] { canvas };
    }

    // -------------------------------------------------------------------------
    // Button handlers
    // -------------------------------------------------------------------------

    private void InsertRowBtn_Click(object sender, RoutedEventArgs e) =>
        ExecuteGridCommandAsync((text, offset) =>
            _gridService.InsertRow(text, offset, _currentGrid!.RowCount)).FireAndForget();

    private void RemoveRowBtn_Click(object sender, RoutedEventArgs e) =>
        ExecuteGridCommandAsync((text, offset) =>
            _gridService.RemoveRow(text, offset, _currentGrid!.RowCount - 1)).FireAndForget();

    private void InsertColBtn_Click(object sender, RoutedEventArgs e) =>
        ExecuteGridCommandAsync((text, offset) =>
            _gridService.InsertColumn(text, offset, _currentGrid!.ColumnCount)).FireAndForget();

    private void RemoveColBtn_Click(object sender, RoutedEventArgs e) =>
        ExecuteGridCommandAsync((text, offset) =>
            _gridService.RemoveColumn(text, offset, _currentGrid!.ColumnCount - 1)).FireAndForget();

    private async Task ExecuteGridCommandAsync(Func<string, int, string> command)
    {
        if (_currentGrid is null) return;

        var docView = await VS.Documents.GetActiveDocumentViewAsync();
        if (docView?.TextView is null) return;

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var snapshot = docView.TextView.TextSnapshot;
        var caretPos = docView.TextView.Caret.Position.BufferPosition.Position;
        var text     = snapshot.GetText();
        var newText  = command(text, caretPos);

        var buffer = docView.TextView.TextBuffer;
        using var edit = buffer.CreateEdit();
        edit.Replace(0, buffer.CurrentSnapshot.Length, newText);
        edit.Apply();

        await RefreshGridDisplayAsync();
    }
}
