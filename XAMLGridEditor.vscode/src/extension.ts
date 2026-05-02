import * as vscode from 'vscode';
import { GridEditorPanel } from './gridEditorPanel';
import { findGridAtOffset } from './xamlParser';
import { GridInfo } from './models';
import { insertRow, insertColumn, removeRow, removeColumn } from './gridManipulator';

function isXamlDocument(doc: vscode.TextDocument): boolean {
    return doc.languageId === 'xml' || doc.languageId === 'xaml' || doc.fileName.endsWith('.xaml');
}

/**
 * Returns the row/column index at the caret, mirroring GetDefinitionIndexAtCaret
 * from XAMLGridEditor.Services.XamlEditorService.
 * Returns -1 if the caret is not on any definition or child element.
 */
function getDefinitionIndexAtCaret(grid: GridInfo, isRow: boolean, caretOffset: number): number {
    const definitions = isRow ? grid.rows : grid.columns;
    for (let i = 0; i < definitions.length; i++) {
        const def = definitions[i];
        if (caretOffset >= def.startOffset && caretOffset <= def.endOffset) {
            return i;
        }
    }
    for (const child of grid.children) {
        if (caretOffset >= child.startOffset && caretOffset <= child.endOffset) {
            return isRow ? child.row : child.column;
        }
    }
    return -1;
}

async function applyGridCommand(
    editor: vscode.TextEditor,
    action: (xaml: string, grid: GridInfo, offset: number) => string | { xaml: string; warnings: string[] }
): Promise<void> {
    const xaml = editor.document.getText();
    const offset = editor.document.offsetAt(editor.selection.active);
    const grid = findGridAtOffset(xaml, offset);
    if (!grid) {
        vscode.window.showInformationMessage('XAML Grid Editor: No Grid at cursor position.');
        return;
    }
    const outcome = action(xaml, grid, offset);
    const newXaml = typeof outcome === 'string' ? outcome : outcome.xaml;
    const warnings = typeof outcome === 'string' ? [] : outcome.warnings;

    if (newXaml !== xaml) {
        const edit = new vscode.WorkspaceEdit();
        edit.replace(
            editor.document.uri,
            new vscode.Range(editor.document.positionAt(0), editor.document.positionAt(xaml.length)),
            newXaml
        );
        await vscode.workspace.applyEdit(edit);
    }
    if (warnings.length > 0) {
        vscode.window.showWarningMessage(warnings.join('\n'));
    }
}

export function activate(context: vscode.ExtensionContext): void {
    const provider = new GridEditorPanel(context.extensionUri);

    context.subscriptions.push(
        vscode.window.registerWebviewViewProvider(GridEditorPanel.viewType, provider)
    );

    function updateContextKey(editor: vscode.TextEditor): void {
        const xaml = editor.document.getText();
        const offset = editor.document.offsetAt(editor.selection.active);
        const hasGrid = findGridAtOffset(xaml, offset) !== null;
        vscode.commands.executeCommand('setContext', 'xamlGridEditor.hasGridAtCursor', hasGrid);
    }

    // Debounce cursor/selection changes to avoid excessive re-parsing
    let refreshTimer: ReturnType<typeof setTimeout> | undefined;

    function scheduleRefresh(editor: vscode.TextEditor): void {
        if (refreshTimer) { clearTimeout(refreshTimer); }
        refreshTimer = setTimeout(() => {
            provider.refresh(editor);
            updateContextKey(editor);
        }, 150);
    }

    context.subscriptions.push(
        vscode.window.onDidChangeTextEditorSelection(e => {
            if (isXamlDocument(e.textEditor.document)) {
                scheduleRefresh(e.textEditor);
            } else {
                vscode.commands.executeCommand('setContext', 'xamlGridEditor.hasGridAtCursor', false);
            }
        })
    );

    context.subscriptions.push(
        vscode.window.onDidChangeActiveTextEditor(editor => {
            if (refreshTimer) { clearTimeout(refreshTimer); }
            if (editor && isXamlDocument(editor.document)) {
                provider.refresh(editor);
                updateContextKey(editor);
            } else {
                provider.clear();
                vscode.commands.executeCommand('setContext', 'xamlGridEditor.hasGridAtCursor', false);
            }
        })
    );

    // Initial refresh if a XAML file is already open
    const active = vscode.window.activeTextEditor;
    if (active && isXamlDocument(active.document)) {
        provider.refresh(active);
        updateContextKey(active);
    }

    // -----------------------------------------------------------------------
    // Context-menu commands — mirrors the VS extension's XAML Grid Editor submenu
    // -----------------------------------------------------------------------

    context.subscriptions.push(
        vscode.commands.registerCommand('xamlGridEditor.insertRowBefore', async () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor || !isXamlDocument(editor.document)) { return; }
            await applyGridCommand(editor, (xaml, grid, offset) => {
                const index = getDefinitionIndexAtCaret(grid, true, offset);
                return insertRow(xaml, grid, index < 0 ? 0 : index);
            });
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('xamlGridEditor.insertColumnBefore', async () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor || !isXamlDocument(editor.document)) { return; }
            await applyGridCommand(editor, (xaml, grid, offset) => {
                const index = getDefinitionIndexAtCaret(grid, false, offset);
                return insertColumn(xaml, grid, index < 0 ? 0 : index);
            });
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('xamlGridEditor.removeCurrentRow', async () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor || !isXamlDocument(editor.document)) { return; }
            await applyGridCommand(editor, (xaml, grid, offset) => {
                const index = getDefinitionIndexAtCaret(grid, true, offset);
                return removeRow(xaml, grid, index < 0 ? 0 : index);
            });
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('xamlGridEditor.removeCurrentColumn', async () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor || !isXamlDocument(editor.document)) { return; }
            await applyGridCommand(editor, (xaml, grid, offset) => {
                const index = getDefinitionIndexAtCaret(grid, false, offset);
                return removeColumn(xaml, grid, index < 0 ? 0 : index);
            });
        })
    );
}

export function deactivate(): void {
    // Nothing to clean up
}
