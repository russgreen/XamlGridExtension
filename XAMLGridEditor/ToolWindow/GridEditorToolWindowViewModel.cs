using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using XAMLGridEditor.Core;
using XAMLGridEditor.Services;

namespace XAMLGridEditor.ToolWindow;

/// <summary>
/// View model for the XAML Grid Editor tool window.
/// </summary>
internal sealed class GridEditorToolWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AsyncPackage _package;
    private readonly XamlEditorService _editorService;
    private readonly GridCaretMonitor _monitor;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public GridEditorToolWindowViewModel(AsyncPackage package, XamlEditorService editorService)
    {
        _package = package;
        _editorService = editorService;

        InsertRowBeforeCommand    = new RelayCommand(ExecuteInsertRowBefore,    () => SelectedRow != null);
        InsertRowAfterCommand     = new RelayCommand(ExecuteInsertRowAfter,     () => SelectedRow != null);
        RemoveRowCommand          = new RelayCommand(ExecuteRemoveRow,          () => SelectedRow != null && Rows.Count > 1);
        InsertColumnBeforeCommand = new RelayCommand(ExecuteInsertColumnBefore, () => SelectedColumn != null);
        InsertColumnAfterCommand  = new RelayCommand(ExecuteInsertColumnAfter,  () => SelectedColumn != null);
        RemoveColumnCommand       = new RelayCommand(ExecuteRemoveColumn,       () => SelectedColumn != null && Columns.Count > 1);

        _monitor = new GridCaretMonitor(editorService);
        _monitor.Changed += (s, e) => Refresh();

        // Initial population.
        ThreadHelper.ThrowIfNotOnUIThread();
        Refresh();
    }

    public void Dispose() => _monitor.Dispose();

    // -----------------------------------------------------------------------
    // Bindable properties
    // -----------------------------------------------------------------------

    private string _gridSummary = "No grid at caret.";
    public string GridSummary
    {
        get => _gridSummary;
        private set => Set(ref _gridSummary, value);
    }

    public ObservableCollection<DefinitionItem> Rows    { get; } = new();
    public ObservableCollection<DefinitionItem> Columns { get; } = new();

    private DefinitionItem? _selectedRow;
    public DefinitionItem? SelectedRow
    {
        get => _selectedRow;
        set { Set(ref _selectedRow, value); ((RelayCommand)InsertRowBeforeCommand).RaiseCanExecuteChanged(); ((RelayCommand)InsertRowAfterCommand).RaiseCanExecuteChanged(); ((RelayCommand)RemoveRowCommand).RaiseCanExecuteChanged(); }
    }

    private DefinitionItem? _selectedColumn;
    public DefinitionItem? SelectedColumn
    {
        get => _selectedColumn;
        set { Set(ref _selectedColumn, value); ((RelayCommand)InsertColumnBeforeCommand).RaiseCanExecuteChanged(); ((RelayCommand)InsertColumnAfterCommand).RaiseCanExecuteChanged(); ((RelayCommand)RemoveColumnCommand).RaiseCanExecuteChanged(); }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set { Set(ref _statusMessage, value); OnPropertyChanged(nameof(StatusVisibility)); }
    }

    public Visibility StatusVisibility =>
        string.IsNullOrEmpty(_statusMessage) ? Visibility.Collapsed : Visibility.Visible;

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    public ICommand InsertRowBeforeCommand    { get; }
    public ICommand InsertRowAfterCommand     { get; }
    public ICommand RemoveRowCommand          { get; }
    public ICommand InsertColumnBeforeCommand { get; }
    public ICommand InsertColumnAfterCommand  { get; }
    public ICommand RemoveColumnCommand       { get; }

    // -----------------------------------------------------------------------
    // Refresh (call on caret change)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Refreshes the displayed grid info from the current caret position.
    /// Must be called on the UI thread.
    /// </summary>
    public void Refresh()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        StatusMessage = string.Empty;

        var grid = _editorService.GetCurrentGrid();
        if (grid is null)
        {
            GridSummary = "No grid at caret.";
            Rows.Clear();
            Columns.Clear();
            return;
        }

        GridSummary = $"{grid.RowCount} row{(grid.RowCount == 1 ? "" : "s")} × {grid.ColumnCount} column{(grid.ColumnCount == 1 ? "" : "s")}";

        Rows.Clear();
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            var item = new DefinitionItem(i, grid.Rows[i].Size);
            item.SizeCommitted += OnRowSizeCommitted;
            Rows.Add(item);
        }

        Columns.Clear();
        for (int i = 0; i < grid.Columns.Count; i++)
        {
            var item = new DefinitionItem(i, grid.Columns[i].Size);
            item.SizeCommitted += OnColumnSizeCommitted;
            Columns.Add(item);
        }
    }

    // -----------------------------------------------------------------------
    // Command implementations
    // -----------------------------------------------------------------------

    private void ExecuteInsertRowBefore()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = _editorService.GetCurrentGrid();
        if (grid is null || SelectedRow is null) return;
        _editorService.InsertRowBefore(grid, SelectedRow.Index);
        Refresh();
        _editorService.FocusEditor();
    }

    private void ExecuteInsertRowAfter()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = _editorService.GetCurrentGrid();
        if (grid is null || SelectedRow is null) return;
        _editorService.InsertRowBefore(grid, SelectedRow.Index + 1);
        Refresh();
        _editorService.FocusEditor();
    }

    private void ExecuteRemoveRow()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = _editorService.GetCurrentGrid();
        if (grid is null || SelectedRow is null) return;
        _editorService.RemoveRow(grid, SelectedRow.Index);
        Refresh();
        _editorService.FocusEditor();
    }

    private void ExecuteInsertColumnBefore()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = _editorService.GetCurrentGrid();
        if (grid is null || SelectedColumn is null) return;
        _editorService.InsertColumnBefore(grid, SelectedColumn.Index);
        Refresh();
        _editorService.FocusEditor();
    }

    private void ExecuteInsertColumnAfter()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = _editorService.GetCurrentGrid();
        if (grid is null || SelectedColumn is null) return;
        _editorService.InsertColumnBefore(grid, SelectedColumn.Index + 1);
        Refresh();
        _editorService.FocusEditor();
    }

    private void ExecuteRemoveColumn()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var grid = _editorService.GetCurrentGrid();
        if (grid is null || SelectedColumn is null) return;
        _editorService.RemoveColumn(grid, SelectedColumn.Index);
        Refresh();
        _editorService.FocusEditor();
    }

    // -----------------------------------------------------------------------
    // Size-change handlers (called when user commits an inline edit)
    // -----------------------------------------------------------------------

    private void OnRowSizeCommitted(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not DefinitionItem item || string.IsNullOrWhiteSpace(item.Size)) return;
        var grid = _editorService.GetCurrentGrid();
        if (grid is null) return;
        _editorService.SetRowSize(grid, item.Index, item.Size);
        Refresh();
    }

    private void OnColumnSizeCommitted(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not DefinitionItem item || string.IsNullOrWhiteSpace(item.Size)) return;
        var grid = _editorService.GetCurrentGrid();
        if (grid is null) return;
        _editorService.SetColumnSize(grid, item.Index, item.Size);
        Refresh();
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>
/// Represents a single row or column definition for display in the tool window list.
/// Implements <see cref="INotifyPropertyChanged"/> so an inline TextBox can bind
/// to <see cref="Size"/>; raises <see cref="SizeCommitted"/> when the user commits
/// a new value so the ViewModel can apply it to the XAML document.
/// </summary>
internal sealed class DefinitionItem : INotifyPropertyChanged
{
    private string _size;

    public int Index { get; }

    public string Size
    {
        get => _size;
        set
        {
            if (_size == value) return;
            _size = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Size)));
        }
    }

    public DefinitionItem(int index, string size)
    {
        Index = index;
        _size = size;
    }

    /// <summary>
    /// Raised by the view's code-behind (LostFocus / Enter) when the user
    /// commits a new size value. The ViewModel subscribes and applies to XAML.
    /// </summary>
    public event EventHandler? SizeCommitted;

    internal void CommitSize() => SizeCommitted?.Invoke(this, EventArgs.Empty);

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Simple relay command implementation.
/// </summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
