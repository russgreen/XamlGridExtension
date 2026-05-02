import * as vscode from 'vscode';
import { GridInfo, rowCount, columnCount } from './models';
import { findGridAtOffset } from './xamlParser';
import { insertRow, insertColumn, removeRow, removeColumn, setRowSize, setColumnSize } from './gridManipulator';

// ---------------------------------------------------------------------------
// WebviewViewProvider
// ---------------------------------------------------------------------------

export class GridEditorPanel implements vscode.WebviewViewProvider {
    public static readonly viewType = 'xamlGridEditor.panel';

    private _view?: vscode.WebviewView;
    private _currentGrid?: GridInfo;
    private _currentDocument?: vscode.TextDocument;
    /** Cache key to avoid unnecessary HTML rebuilds when grid hasn't changed. */
    private _lastGridKey?: string;
    private readonly _log: vscode.OutputChannel;

    constructor(private readonly _extensionUri: vscode.Uri) {
        this._log = vscode.window.createOutputChannel('XAML Grid Editor');
    }

    resolveWebviewView(
        webviewView: vscode.WebviewView,
        _context: vscode.WebviewViewResolveContext,
        _token: vscode.CancellationToken
    ): void {
        this._log.appendLine(`resolveWebviewView: currentGrid=${this._currentGrid ? 'set' : 'null'}`);
        this._view = webviewView;
        webviewView.webview.options = { enableScripts: true };
        // Force rebuild on next _sendUpdate so the new webview gets current state.
        this._lastGridKey = undefined;
        webviewView.webview.html = this._buildHtml(webviewView.webview);
        webviewView.webview.onDidReceiveMessage(msg => this._handleMessage(msg));

        webviewView.onDidChangeVisibility(() => {
            this._log.appendLine(`onDidChangeVisibility: visible=${webviewView.visible}`);
            if (webviewView.visible) {
                this._lastGridKey = undefined; // force rebuild
                this._sendUpdate();
            }
        });
    }

    /** Called when the active XAML editor or cursor position changes. */
    refresh(editor: vscode.TextEditor): void {
        const xaml = editor.document.getText();
        const offset = editor.document.offsetAt(editor.selection.active);
        this._currentDocument = editor.document;
        this._currentGrid = findGridAtOffset(xaml, offset) ?? undefined;
        this._log.appendLine(`refresh: offset=${offset}, grid=${this._currentGrid ? `rows=${this._currentGrid.rows.length} cols=${this._currentGrid.columns.length}` : 'null'}, viewSet=${!!this._view}`);
        this._sendUpdate();
    }

    /** Clear the panel (no XAML file active). */
    clear(): void {
        this._currentGrid = undefined;
        this._currentDocument = undefined;
        this._lastGridKey = undefined;
        this._sendUpdate();
    }

    // -----------------------------------------------------------------------
    // Message handling (commands from webview buttons)
    // -----------------------------------------------------------------------

    private async _handleMessage(message: { command: string; index: number; size?: string }): Promise<void> {
        if (!this._currentGrid || !this._currentDocument) { return; }

        const doc = this._currentDocument;
        let xaml = doc.getText();
        let result: string | undefined;
        let warnings: string[] = [];

        switch (message.command) {
            case 'insertRowBefore':
                result = insertRow(xaml, this._currentGrid, message.index);
                break;
            case 'insertRowAfter':
                result = insertRow(xaml, this._currentGrid, message.index + 1);
                break;
            case 'addRow':
                result = insertRow(xaml, this._currentGrid, rowCount(this._currentGrid));
                break;
            case 'removeRow': {
                const r = removeRow(xaml, this._currentGrid, message.index);
                result = r.xaml; warnings = r.warnings;
                break;
            }
            case 'insertColumnBefore':
                result = insertColumn(xaml, this._currentGrid, message.index);
                break;
            case 'insertColumnAfter':
                result = insertColumn(xaml, this._currentGrid, message.index + 1);
                break;
            case 'addColumn':
                result = insertColumn(xaml, this._currentGrid, columnCount(this._currentGrid));
                break;
            case 'removeColumn': {
                const r = removeColumn(xaml, this._currentGrid, message.index);
                result = r.xaml; warnings = r.warnings;
                break;
            }
            case 'setRowSize':
                if (message.size) { result = setRowSize(xaml, this._currentGrid, message.index, message.size); }
                break;
            case 'setColumnSize':
                if (message.size) { result = setColumnSize(xaml, this._currentGrid, message.index, message.size); }
                break;
        }

        if (result !== undefined && result !== xaml) {
            const edit = new vscode.WorkspaceEdit();
            edit.replace(
                doc.uri,
                new vscode.Range(doc.positionAt(0), doc.positionAt(xaml.length)),
                result
            );
            await vscode.workspace.applyEdit(edit);

            // Refresh panel after edit
            const activeEditor = vscode.window.visibleTextEditors.find(
                e => e.document.uri.toString() === doc.uri.toString()
            );
            if (activeEditor) { this.refresh(activeEditor); }
        }

        if (warnings.length > 0) {
            vscode.window.showWarningMessage(warnings.join('\n'));
        }
    }

    // -----------------------------------------------------------------------
    // State → HTML (server-side render, no postMessage for updates)
    // -----------------------------------------------------------------------

    private _sendUpdate(): void {
        if (!this._view) {
            this._log.appendLine('_sendUpdate: no view, skipping');
            return;
        }
        const grid = this._currentGrid;
        // Build a cache key that captures the structure. Include row/column sizes so
        // edits to definitions trigger a rebuild.
        const key = grid
            ? `${grid.startOffset}-${grid.endOffset}-${grid.rows.map(r => r.size).join(',')}-${grid.columns.map(c => c.size).join(',')}`
            : 'null';

        if (key === this._lastGridKey) {
            this._log.appendLine('_sendUpdate: no change, skipping rebuild');
            return;
        }
        this._lastGridKey = key;
        this._log.appendLine(`_sendUpdate: rebuilding HTML, grid=${grid ? `rows=${grid.rows.length} cols=${grid.columns.length}` : 'null'}, visible=${this._view.visible}`);
        this._view.webview.html = this._buildHtml(this._view.webview);
    }

    // -----------------------------------------------------------------------
    // Webview HTML (grid state is baked directly into the HTML — no postMessage)
    // -----------------------------------------------------------------------

    private _buildHtml(webview: vscode.Webview): string {
        const nonce = getNonce();
        const grid = this._currentGrid;

        const bodyContent = grid
            ? this._renderGrid(grid)
            : '<p class="no-grid">No grid at cursor position.</p>';

        return /* html */ `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8"/>
  <meta http-equiv="Content-Security-Policy"
        content="default-src 'none'; style-src 'nonce-${nonce}'; script-src 'nonce-${nonce}';"/>
  <meta name="viewport" content="width=device-width,initial-scale=1"/>
  <style nonce="${nonce}">
    *{box-sizing:border-box;margin:0;padding:0}
    body{
      font-family:var(--vscode-font-family);
      font-size:var(--vscode-font-size);
      color:var(--vscode-foreground);
      background:transparent;
      padding:8px;
    }
    h3{
      font-size:0.85em;
      text-transform:uppercase;
      letter-spacing:0.08em;
      opacity:0.65;
      margin:12px 0 6px;
    }
    h3:first-of-type{margin-top:0}
    .summary{
      font-weight:600;
      margin-bottom:10px;
      font-size:0.95em;
    }
    .def-list{display:flex;flex-direction:column;gap:3px;margin-bottom:6px}
    .def-row{
      display:flex;
      align-items:center;
      gap:4px;
      padding:2px 0;
    }
    .def-label{
      min-width:52px;
      font-size:0.88em;
      opacity:0.75;
      flex-shrink:0;
    }
    .size-input{
      width:64px;
      background:var(--vscode-input-background);
      color:var(--vscode-input-foreground);
      border:1px solid var(--vscode-input-border,rgba(128,128,128,0.35));
      padding:2px 5px;
      font-family:var(--vscode-font-family);
      font-size:var(--vscode-font-size);
      border-radius:2px;
      outline:none;
    }
    .size-input:focus{border-color:var(--vscode-focusBorder)}
    .btn{
      background:var(--vscode-button-secondaryBackground,rgba(128,128,128,0.15));
      color:var(--vscode-button-secondaryForeground,var(--vscode-foreground));
      border:none;
      padding:2px 7px;
      cursor:pointer;
      font-size:0.82em;
      border-radius:2px;
      line-height:1.6;
    }
    .btn:hover{background:var(--vscode-button-secondaryHoverBackground,rgba(128,128,128,0.25))}
    .btn:disabled{opacity:0.4;cursor:default}
    .btn-primary{
      background:var(--vscode-button-background);
      color:var(--vscode-button-foreground);
    }
    .btn-primary:hover{background:var(--vscode-button-hoverBackground)}
    .btn-danger{
      background:transparent;
      color:var(--vscode-errorForeground,#f44);
      border:1px solid var(--vscode-errorForeground,#f44);
    }
    .btn-danger:hover{background:rgba(200,0,0,0.15)}
    .add-row{margin-top:4px}
    .no-grid{opacity:0.55;font-style:italic;margin-top:8px}
    .implicit-note{opacity:0.6;font-size:0.88em;margin-bottom:4px}
  </style>
</head>
<body>
  <div id="root">${bodyContent}</div>
  <script nonce="${nonce}">
    const vscode = acquireVsCodeApi();

    // Use event delegation — avoids inline onclick handlers blocked by CSP.
    document.addEventListener('click', function(e) {
      const btn = e.target && e.target.closest('button[data-cmd]');
      if (!btn || btn.disabled) { return; }
      const cmd = btn.getAttribute('data-cmd');
      const idx = parseInt(btn.getAttribute('data-index') || '0', 10);
      vscode.postMessage({ command: cmd, index: idx });
    });

    // Attach change listeners to size inputs.
    document.querySelectorAll('.size-input').forEach(function(input) {
      input.addEventListener('change', commitSize);
      input.addEventListener('keydown', function(e) {
        if (e.key === 'Enter') { e.preventDefault(); commitSize.call(e.target, e); }
      });
    });

    function commitSize(e) {
      var el = e.target || this;
      var type = el.getAttribute('data-type');
      var index = parseInt(el.getAttribute('data-index'), 10);
      var size = el.value.trim();
      if (!size) { return; }
      vscode.postMessage({ command: type === 'row' ? 'setRowSize' : 'setColumnSize', index: index, size: size });
    }
  </script>
</body>
</html>`;
    }

    private _renderGrid(grid: GridInfo): string {
        const canRemoveRow = grid.rows.length > 1;
        const canRemoveCol = grid.columns.length > 1;
        const rCount = rowCount(grid);
        const cCount = columnCount(grid);

        let html = `<div class="summary">${rCount} row${rCount !== 1 ? 's' : ''} &times; ${cCount} column${cCount !== 1 ? 's' : ''}</div>`;

        // Rows
        html += '<h3>Rows</h3>';
        if (grid.rows.length === 0) {
            html += '<p class="implicit-note">1 implicit row &mdash; no definitions</p>';
        } else {
            html += '<div class="def-list">';
            for (let i = 0; i < grid.rows.length; i++) {
                const r = grid.rows[i];
                html += `<div class="def-row">
  <span class="def-label">Row ${i}</span>
  <input class="size-input" type="text" value="${escHtml(r.size)}" data-type="row" data-index="${i}" title="Height (e.g. *, Auto, 100)"/>
  <button class="btn" data-cmd="insertRowBefore" data-index="${i}" title="Insert row before">&#8593;</button>
  <button class="btn" data-cmd="insertRowAfter" data-index="${i}" title="Insert row after">&#8595;</button>
  <button class="btn btn-danger" data-cmd="removeRow" data-index="${i}" ${canRemoveRow ? '' : 'disabled'} title="Remove row">&times;</button>
</div>`;
            }
            html += '</div>';
        }
        html += `<button class="btn btn-primary add-row" data-cmd="addRow" data-index="-1">+ Add Row</button>`;

        // Columns
        html += '<h3>Columns</h3>';
        if (grid.columns.length === 0) {
            html += '<p class="implicit-note">1 implicit column &mdash; no definitions</p>';
        } else {
            html += '<div class="def-list">';
            for (let i = 0; i < grid.columns.length; i++) {
                const c = grid.columns[i];
                html += `<div class="def-row">
  <span class="def-label">Col ${i}</span>
  <input class="size-input" type="text" value="${escHtml(c.size)}" data-type="col" data-index="${i}" title="Width (e.g. *, Auto, 100)"/>
  <button class="btn" data-cmd="insertColumnBefore" data-index="${i}" title="Insert column before">&#8592;</button>
  <button class="btn" data-cmd="insertColumnAfter" data-index="${i}" title="Insert column after">&#8594;</button>
  <button class="btn btn-danger" data-cmd="removeColumn" data-index="${i}" ${canRemoveCol ? '' : 'disabled'} title="Remove column">&times;</button>
</div>`;
            }
            html += '</div>';
        }
        html += `<button class="btn btn-primary add-row" data-cmd="addColumn" data-index="-1">+ Add Column</button>`;

        return html;
    }
}

function escHtml(s: string): string {
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

function getNonce(): string {
    let text = '';
    const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
    for (let i = 0; i < 32; i++) {
        text += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    return text;
}
