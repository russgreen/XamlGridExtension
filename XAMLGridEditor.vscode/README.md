# XAML Grid Editor

A VSCode extension for editing XAML `Grid` layouts without manually renumbering child elements.

`XAML Grid Editor` inserts and removes rows and columns, updates `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, and `Grid.ColumnSpan` on affected children, and provides a tool window for editing grid structure directly from the IDE.

## Features

- Insert rows and columns into an existing XAML `Grid`
- Remove rows and columns and automatically shift affected child indices
- Update `Grid.RowSpan` and `Grid.ColumnSpan` when structure changes require it
- Edit row heights and column widths from a dedicated tool window
- Enable commands only when the caret is inside a `Grid`
- Work with multiple XAML dialects by matching elements by local name rather than framework-specific namespaces

## Supported XAML dialects

The parser is namespace-agnostic, so the extension is intended to work with XAML that uses `Grid`, `RowDefinition`, and `ColumnDefinition` semantics, including:

- WPF
- WinUI
- .NET MAUI
- Avalonia
- Other compatible XAML dialects

> [!NOTE]
> The extension runs inside VSCode and operates on the active XAML document. Support depends on the document being valid enough to parse as XAML.

## How it works

When the caret is inside a `Grid`, the extension locates the innermost grid at the current cursor position, parses its row and column definitions, and applies text edits to the active document.

Structure changes are applied in two stages:

1. Update child attached properties such as `Grid.Row` and `Grid.Column`
2. Insert, remove, or resize the corresponding row or column definition

This keeps changes undoable and aligned with the current editor buffer.

## Usage

### Context menu commands

In the XAML editor, right-click while the caret is inside a `Grid` to access:

- `Insert Row Before`
- `Insert Column Before`
- `Remove Current Row`
- `Remove Current Column`

These commands are hidden when the caret is not inside a grid.

### Tool window

Open the tool window from:

- `View` -> `XAML Grid Editor`

The tool window shows the current grid summary and lets you:

- Select a row or column
- Insert before or after the selected item
- Remove the selected item
- Edit row `Height` values inline
- Edit column `Width` values inline

## Example

Given a grid like this:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
    </Grid.RowDefinitions>

    <TextBlock Text="Header" />
    <TextBox Grid.Row="1" Text="Body" />
</Grid>
```

Inserting a row before row `1` updates the grid definitions and shifts children automatically:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="*"/>
    </Grid.RowDefinitions>

    <TextBlock Text="Header" />
    <TextBox Grid.Row="2" Text="Body" />
</Grid>
```

