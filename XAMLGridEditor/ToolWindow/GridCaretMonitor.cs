using System;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using XAMLGridEditor.Services;

namespace XAMLGridEditor.ToolWindow;

/// <summary>
/// Monitors caret position changes in the active editor and fires
/// <see cref="Changed"/> (debounced) whenever the caret moves.
/// Also detects when the active document changes (polls every second).
/// </summary>
internal sealed class GridCaretMonitor : IDisposable
{
    private readonly XamlEditorService _editorService;

    // Fired on the UI thread after debounce; ViewModel calls Refresh() in response.
    public event EventHandler? Changed;

    private IWpfTextView? _subscribedView;

    // Debounce: delay actual refresh until the caret has been still for 300 ms.
    private readonly DispatcherTimer _debounceTimer;

    // Document-switch detection: check every second whether the active view changed.
    private readonly DispatcherTimer _switchTimer;

    public GridCaretMonitor(XamlEditorService editorService)
    {
        _editorService = editorService;

        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            Changed?.Invoke(this, EventArgs.Empty);
        };

        _switchTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _switchTimer.Tick += OnSwitchTimerTick;
        _switchTimer.Start();
    }

    public void Dispose()
    {
        _switchTimer.Stop();
        _debounceTimer.Stop();
        UnsubscribeFromView(_subscribedView);
        _subscribedView = null;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void OnSwitchTimerTick(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var view = _editorService.GetActiveView();
        if (view == _subscribedView) return;

        UnsubscribeFromView(_subscribedView);
        _subscribedView = view;
        SubscribeToView(view);

        // Refresh immediately when switching documents.
        FireDebounced();
    }

    private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        => FireDebounced();

    private void FireDebounced()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void SubscribeToView(IWpfTextView? view)
    {
        if (view != null)
            view.Caret.PositionChanged += OnCaretPositionChanged;
    }

    private void UnsubscribeFromView(IWpfTextView? view)
    {
        if (view != null)
            view.Caret.PositionChanged -= OnCaretPositionChanged;
    }
}
