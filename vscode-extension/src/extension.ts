import * as vscode from 'vscode';
import { GridEditorPanel } from './gridEditorPanel';

function isXamlDocument(doc: vscode.TextDocument): boolean {
    return doc.languageId === 'xml' || doc.languageId === 'xaml' || doc.fileName.endsWith('.xaml');
}

export function activate(context: vscode.ExtensionContext): void {
    const provider = new GridEditorPanel(context.extensionUri);

    context.subscriptions.push(
        vscode.window.registerWebviewViewProvider(GridEditorPanel.viewType, provider, {
            webviewOptions: { retainContextWhenHidden: true },
        })
    );

    // Debounce cursor/selection changes to avoid excessive re-parsing
    let refreshTimer: ReturnType<typeof setTimeout> | undefined;

    function scheduleRefresh(editor: vscode.TextEditor): void {
        if (refreshTimer) { clearTimeout(refreshTimer); }
        refreshTimer = setTimeout(() => { provider.refresh(editor); }, 150);
    }

    context.subscriptions.push(
        vscode.window.onDidChangeTextEditorSelection(e => {
            if (isXamlDocument(e.textEditor.document)) {
                scheduleRefresh(e.textEditor);
            }
        })
    );

    context.subscriptions.push(
        vscode.window.onDidChangeActiveTextEditor(editor => {
            if (refreshTimer) { clearTimeout(refreshTimer); }
            if (editor && isXamlDocument(editor.document)) {
                provider.refresh(editor);
            } else {
                provider.clear();
            }
        })
    );

    // Initial refresh if a XAML file is already open
    const active = vscode.window.activeTextEditor;
    if (active && isXamlDocument(active.document)) {
        provider.refresh(active);
    }
}

export function deactivate(): void {
    // Nothing to clean up
}
