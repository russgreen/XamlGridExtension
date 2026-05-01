import * as vscode from 'vscode';
import { GridInfo, rowCount, columnCount } from './models';
import { findGridAtOffset } from './xamlParser';
import { insertRow, insertColumn, removeRow, removeColumn, setRowSize, setColumnSize } from './gridManipulator';

// ---------------------------------------------------------------------------
// Types exchanged between extension and webview
// ---------------------------------------------------------------------------

interface GridDisplay {
    rowCount: number;
    columnCount: number;
    rows: Array<{ index: number; size: string }>;
    columns: Array<{ index: number; size: string }>;
}

// ---------------------------------------------------------------------------
// WebviewViewProvider
// ---------------------------------------------------------------------------

export class GridEditorPanel implements vscode.WebviewViewProvider {
    public static readonly viewType = 'xamlGridEditor.panel';

    private _view?: vscode.WebviewView;
    private _currentGrid?: GridInfo;
    private _currentDocument?: vscode.TextDocument;

    constructor(private readonly _extensionUri: vscode.Uri) {}

    resolveWebviewView(
        webviewView: vscode.WebviewView,
        _context: vscode.WebviewViewResolveContext,
        _token: vscode.CancellationToken
    ): void {
        this._view = webviewView;
        webviewView.webview.options = { enableScripts: true };
        webviewView.webview.html = this._buildHtml(webviewView.webview);
        webviewView.webview.onDidReceiveMessage(msg => this._handleMessage(msg));
    }

    /** Called when the active XAML editor or cursor position changes. */
    refresh(editor: vscode.TextEditor): void {
        if (!this._view) { return; }
        const xaml = editor.document.getText();
        const offset = editor.document.offsetAt(editor.selection.active);
        this._currentDocument = editor.document;
        this._currentGrid = findGridAtOffset(xaml, offset) ?? undefined;
        this._sendUpdate();
    }

    /** Clear the panel (no XAML file active). */
    clear(): void {
        this._currentGrid = undefined;
        this._currentDocument = undefined;
        if (this._view) {
            this._view.webview.postMessage({ command: 'update', grid: null });
        }
    }

    // -----------------------------------------------------------------------
    // Message handling
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
    // Internal helpers
    // -----------------------------------------------------------------------

    private _sendUpdate(): void {
        if (!this._view) { return; }
        const grid = this._currentGrid;
        const payload: GridDisplay | null = grid
            ? {
                rowCount: rowCount(grid),
                columnCount: columnCount(grid),
                rows: grid.rows.map((r, i) => ({ index: i, size: r.size })),
                columns: grid.columns.map((c, i) => ({ index: i, size: c.size })),
            }
            : null;
        this._view.webview.postMessage({ command: 'update', grid: payload });
    }

    // -----------------------------------------------------------------------
    // Webview HTML
    // -----------------------------------------------------------------------

    private _buildHtml(webview: vscode.Webview): string {
        const nonce = getNonce();
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
    .size-input:focus{
      border-color:var(--vscode-focusBorder);
    }
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
    .btn:hover{
      background:var(--vscode-button-secondaryHoverBackground,rgba(128,128,128,0.25));
    }
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
    .no-grid{
      opacity:0.55;
      font-style:italic;
      margin-top:8px;
    }
    .implicit-note{
      opacity:0.6;
      font-size:0.88em;
      margin-bottom:4px;
    }
  </style>
</head>
<body>
  <div id="root"><p class="no-grid">No grid at cursor position.</p></div>
  <script nonce="${nonce}">
    const vscode = acquireVsCodeApi();

    window.addEventListener('message', e => {
      const msg = e.data;
      if (msg.command === 'update') render(msg.grid);
    });

    function esc(s) {
      return String(s)
        .replace(/&/g,'&amp;')
        .replace(/</g,'&lt;')
        .replace(/>/g,'&gt;')
        .replace(/"/g,'&quot;');
    }

    function render(grid) {
      const root = document.getElementById('root');
      if (!grid) {
        root.innerHTML = '<p class="no-grid">No grid at cursor position.</p>';
        return;
      }

      const canRemoveRow = grid.rows.length > 1;
      const canRemoveCol = grid.columns.length > 1;

      let html = \`<div class="summary">\${grid.rowCount} row\${grid.rowCount !== 1 ? 's' : ''} &times; \${grid.columnCount} column\${grid.columnCount !== 1 ? 's' : ''}</div>\`;

      // ---- Rows ----
      html += '<h3>Rows</h3>';
      if (grid.rows.length === 0) {
        html += '<p class="implicit-note">1 implicit row &mdash; no definitions</p>';
      } else {
        html += '<div class="def-list">';
        for (const row of grid.rows) {
          html += \`<div class="def-row">
            <span class="def-label">Row \${row.index}</span>
            <input class="size-input" type="text" value="\${esc(row.size)}"
                   data-type="row" data-index="\${row.index}" title="Height (e.g. *, Auto, 100)"/>
            <button class="btn" onclick="send('insertRowBefore',\${row.index})" title="Insert row before">&#8593;</button>
            <button class="btn" onclick="send('insertRowAfter',\${row.index})" title="Insert row after">&#8595;</button>
            <button class="btn btn-danger" onclick="send('removeRow',\${row.index})"
                    \${canRemoveRow ? '' : 'disabled'} title="Remove row">&times;</button>
          </div>\`;
        }
        html += '</div>';
      }
      html += '<button class="btn btn-primary add-row" onclick="addDef(\'row\')">+ Add Row</button>';

      // ---- Columns ----
      html += '<h3>Columns</h3>';
      if (grid.columns.length === 0) {
        html += '<p class="implicit-note">1 implicit column &mdash; no definitions</p>';
      } else {
        html += '<div class="def-list">';
        for (const col of grid.columns) {
          html += \`<div class="def-row">
            <span class="def-label">Col \${col.index}</span>
            <input class="size-input" type="text" value="\${esc(col.size)}"
                   data-type="col" data-index="\${col.index}" title="Width (e.g. *, Auto, 100)"/>
            <button class="btn" onclick="send('insertColumnBefore',\${col.index})" title="Insert column before">&#8592;</button>
            <button class="btn" onclick="send('insertColumnAfter',\${col.index})" title="Insert column after">&#8594;</button>
            <button class="btn btn-danger" onclick="send('removeColumn',\${col.index})"
                    \${canRemoveCol ? '' : 'disabled'} title="Remove column">&times;</button>
          </div>\`;
        }
        html += '</div>';
      }
      html += '<button class="btn btn-primary add-row" onclick="addDef(\'col\')">+ Add Column</button>';

      root.innerHTML = html;

      // Attach change/Enter listeners to editable size fields
      root.querySelectorAll('.size-input').forEach(input => {
        input.addEventListener('change', commitSize);
        input.addEventListener('keydown', e => { if (e.key === 'Enter') commitSize.call(e.target, e); });
      });
    }

    function commitSize(e) {
      const el = e.target || this;
      const type = el.dataset.type;
      const index = parseInt(el.dataset.index, 10);
      const size = el.value.trim();
      if (!size) return;
      vscode.postMessage({ command: type === 'row' ? 'setRowSize' : 'setColumnSize', index, size });
    }

    function send(command, index) {
      vscode.postMessage({ command, index });
    }

    function addDef(type) {
      vscode.postMessage({ command: type === 'row' ? 'addRow' : 'addColumn', index: -1 });
    }
  </script>
</body>
</html>`;
    }
}

function getNonce(): string {
    let text = '';
    const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
    for (let i = 0; i < 32; i++) {
        text += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    return text;
}
