using System;
using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using XamlGridExtension.Services;

namespace XamlGridExtension.Commands;

/// <summary>
/// Base class for Grid commands. Provides shared document access and Grid service.
/// </summary>
internal abstract class GridCommandBase<TCommand> : BaseCommand<TCommand>
    where TCommand : class, new()
{
    protected static readonly IXamlGridService GridService = new XamlGridService();

    /// <summary>
    /// Gets the full text of the active document and the caret offset.
    /// Returns null if no XAML document is active.
    /// </summary>
    protected static async Task<(string text, int caretOffset, DocumentView view)?> GetActiveXamlDocumentAsync()
    {
        var docView = await VS.Documents.GetActiveDocumentViewAsync();
        if (docView?.TextView is null) return null;

        var snapshot = docView.TextView.TextSnapshot;
        var caretPos = docView.TextView.Caret.Position.BufferPosition;

        return (snapshot.GetText(), caretPos.Position, docView);
    }

    protected static async Task ReplaceDocumentTextAsync(DocumentView docView, string newText)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var buffer = docView.TextView!.TextBuffer;
        using var edit = buffer.CreateEdit();
        edit.Replace(0, buffer.CurrentSnapshot.Length, newText);
        edit.Apply();
    }

    protected override Task InitializeCompletedAsync() => Task.CompletedTask;
}
