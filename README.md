# XAML Grid Editor

A Visual Studio extension for editing XAML `Grid` layouts without manually renumbering child elements.

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
> The extension runs inside Visual Studio and operates on the active XAML document. Support depends on the document being valid enough to parse as XML.

## How it works

When the caret is inside a `Grid`, the extension locates the innermost grid at the current cursor position, parses its row and column definitions, and applies text edits to the active document.

Structure changes are applied in two stages:

1. Update child attached properties such as `Grid.Row` and `Grid.Column`
2. Insert, remove, or resize the corresponding row or column definition

This keeps changes undoable and aligned with the current editor buffer.

## Usage

### Context menu commands

In the XAML editor, right-click while the caret is inside a `Grid`, then open the `XAML Grid Editor` submenu:

- `Insert Row Before` (inserts before the `RowDefinition` at the caret, or before row `0` if the caret is not on a row definition)
- `Insert Column Before` (inserts before the `ColumnDefinition` at the caret, or before column `0` if the caret is not on a column definition)
- `Remove Current Row` (currently removes row `0`)
- `Remove Current Column` (currently removes column `0`)

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

## Solution structure

- `XAMLGridEditor` - Visual Studio extension project and tool window UI
- `XAMLGridEditor.Core` - XAML parsing and grid manipulation logic
- `XAMLGridEditor.Tests` - MSTest coverage for parser and manipulation behavior
- `XAMLGridEditor.vscode` - VSCode version of the extension

## Requirements

- Visual Studio 2022 17.14+ (Community/Professional/Enterprise) on amd64, or a compatible newer Visual Studio version matching the VSIX manifest target range
- .NET Framework 4.8 SDK/runtime for building the projects

## Build and run locally

1. Open the solution in Visual Studio.
2. Restore NuGet packages.
3. Build the solution.
4. Start debugging the VSIX project to launch an Experimental Instance of Visual Studio.
5. Open a XAML file in the experimental instance and place the caret inside a `Grid`.

In Debug builds inside Visual Studio, the extension is configured to deploy to the experimental instance automatically.

## Testing

The solution includes automated tests for:

- XAML grid parsing
- Row and column insertion
- Row and column removal

Run the test project from Test Explorer or with your usual .NET test workflow.

## Current scope

This repository contains a **Visual Studio extension (VSIX)** focused on structural `Grid` editing in XAML documents. It does not attempt to be a full visual designer; instead, it provides targeted editing commands that keep grid-related attached properties in sync.

## Repository status

This repository contains the source for the extension, including the Visual Studio package, core manipulation engine, and automated tests. 
