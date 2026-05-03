using Microsoft.VisualStudio.TestTools.UnitTesting;
using XAMLGridEditor.Core;

namespace XAMLGridEditor.Tests;

[TestClass]
public class XamlGridParserTests
{
    // -----------------------------------------------------------------------
    // Shared XAML fixtures
    // -----------------------------------------------------------------------

    private const string WpfSimple = """
        <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Row="0" Grid.Column="0" Text="A"/>
                <TextBlock Grid.Row="1" Grid.Column="1" Text="B"/>
            </Grid>
        </Window>
        """;

    private const string NestedGrids = """
        <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                <Grid Grid.Row="1">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Row="0" Text="Inner"/>
                </Grid>
            </Grid>
        </Window>
        """;

    // -----------------------------------------------------------------------
    // ParseAllGrids
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ParseAllGrids_WpfSimple_FindsTwoTextBlocks()
    {
        var grids = XamlGridParser.ParseAllGrids(WpfSimple);
        Assert.AreEqual(1, grids.Count);
        var grid = grids[0];
        Assert.AreEqual(2, grid.Rows.Count);
        Assert.AreEqual(2, grid.Columns.Count);
        Assert.AreEqual(2, grid.Children.Count);
    }

    [TestMethod]
    public void ParseAllGrids_NestedGrids_FindsBothGrids()
    {
        var grids = XamlGridParser.ParseAllGrids(NestedGrids);
        Assert.AreEqual(2, grids.Count);
    }

    [TestMethod]
    public void ParseAllGrids_InvalidXml_ReturnsEmptyList()
    {
        var grids = XamlGridParser.ParseAllGrids("<this is not valid xml<<<");
        Assert.AreEqual(0, grids.Count);
    }

    [TestMethod]
    public void ParseAllGrids_NoGrid_ReturnsEmptyList()
    {
        var grids = XamlGridParser.ParseAllGrids(
            "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><StackPanel/></Window>");
        Assert.AreEqual(0, grids.Count);
    }

    // -----------------------------------------------------------------------
    // FindGridAtOffset – single grid
    // -----------------------------------------------------------------------

    [TestMethod]
    public void FindGridAtOffset_InsideGrid_ReturnsGrid()
    {
        int offset = WpfSimple.IndexOf("TextBlock");
        var grid = XamlGridParser.FindGridAtOffset(WpfSimple, offset);
        Assert.IsNotNull(grid);
        Assert.AreEqual(2, grid!.Rows.Count);
    }

    [TestMethod]
    public void FindGridAtOffset_BeforeGrid_ReturnsNull()
    {
        int offset = WpfSimple.IndexOf("<Grid>");
        // Just before the opening <Grid>
        var grid = XamlGridParser.FindGridAtOffset(WpfSimple, offset - 1);
        Assert.IsNull(grid);
    }

    // -----------------------------------------------------------------------
    // FindGridAtOffset – nested grids
    // -----------------------------------------------------------------------

    [TestMethod]
    public void FindGridAtOffset_InnerGrid_ReturnsInnermostGrid()
    {
        int offset = NestedGrids.IndexOf("Inner");
        var grid = XamlGridParser.FindGridAtOffset(NestedGrids, offset);
        Assert.IsNotNull(grid);
        // Inner grid has 1 row; outer has 2.
        Assert.AreEqual(1, grid!.Rows.Count, "Should return the inner (smallest) grid.");
    }

    [TestMethod]
    public void FindGridAtOffset_OuterGridArea_ReturnsOuterGrid()
    {
        // Find the outer Grid row 0 area (before the inner Grid starts)
        int offset = NestedGrids.IndexOf("<Grid.RowDefinitions>");
        var grid = XamlGridParser.FindGridAtOffset(NestedGrids, offset + 5);
        Assert.IsNotNull(grid);
        Assert.AreEqual(2, grid!.Rows.Count, "Should return the outer grid.");
    }

    // -----------------------------------------------------------------------
    // Child attribute parsing
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ParseAllGrids_ChildrenHaveCorrectAttributes()
    {
        var grid = XamlGridParser.ParseAllGrids(WpfSimple)[0];

        var childA = grid.Children.Find(c => c.ElementName == "TextBlock" && c.Row == 0 && c.Column == 0);
        Assert.IsNotNull(childA);
        Assert.IsTrue(childA!.HasExplicitRow);
        Assert.IsTrue(childA.HasExplicitColumn);

        var childB = grid.Children.Find(c => c.ElementName == "TextBlock" && c.Row == 1 && c.Column == 1);
        Assert.IsNotNull(childB);
    }

    [TestMethod]
    public void ParseAllGrids_ImplicitRowColumnDefaultToZero()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <TextBlock Text="No explicit row or column"/>
                </Grid>
            </Window>
            """;

        var grid = XamlGridParser.ParseAllGrids(xaml)[0];
        Assert.AreEqual(1, grid.Children.Count);
        var child = grid.Children[0];
        Assert.AreEqual(0, child.Row);
        Assert.AreEqual(0, child.Column);
        Assert.IsFalse(child.HasExplicitRow);
        Assert.IsFalse(child.HasExplicitColumn);
    }

    // -----------------------------------------------------------------------
    // AvaloniaUI dialect
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ParseAllGrids_AvaloniaDialect_Supported()
    {
        const string xaml = """
            <Window xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Row="0" Text="Avalonia"/>
                </Grid>
            </Window>
            """;

        var grids = XamlGridParser.ParseAllGrids(xaml);
        Assert.AreEqual(1, grids.Count);
        Assert.AreEqual(2, grids[0].Rows.Count);
    }

    // -----------------------------------------------------------------------
    // Shorthand attribute syntax (e.g. RowDefinitions="*, Auto")
    // -----------------------------------------------------------------------

    private const string ShorthandXaml = """
        <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid Margin="10"
                  RowDefinitions="*, Auto"
                  ColumnDefinitions="*, 200">
                <TextBlock Grid.Row="0" Grid.Column="0" Text="A"/>
                <TextBlock Grid.Row="1" Grid.Column="1" Text="B"/>
            </Grid>
        </Window>
        """;

    [TestMethod]
    public void ParseAllGrids_ShorthandSyntax_ParsesRowsAndColumns()
    {
        var grids = XamlGridParser.ParseAllGrids(ShorthandXaml);
        Assert.AreEqual(1, grids.Count);
        var grid = grids[0];

        Assert.IsTrue(grid.HasShorthandRowDefinitions, "Should detect shorthand row definitions.");
        Assert.IsTrue(grid.HasShorthandColumnDefinitions, "Should detect shorthand column definitions.");
        Assert.AreEqual(2, grid.Rows.Count);
        Assert.AreEqual(2, grid.Columns.Count);
    }

    [TestMethod]
    public void ParseAllGrids_ShorthandSyntax_ParsesCorrectSizes()
    {
        var grids = XamlGridParser.ParseAllGrids(ShorthandXaml);
        var grid = grids[0];

        Assert.AreEqual("*",    grid.Rows[0].Size);
        Assert.AreEqual("Auto", grid.Rows[1].Size);
        Assert.AreEqual("*",    grid.Columns[0].Size);
        Assert.AreEqual("200",  grid.Columns[1].Size);
    }

    [TestMethod]
    public void ParseAllGrids_ShorthandSyntax_EntriesAreMarkedShorthand()
    {
        var grids = XamlGridParser.ParseAllGrids(ShorthandXaml);
        var grid = grids[0];

        Assert.IsTrue(grid.Rows[0].IsShorthand);
        Assert.IsTrue(grid.Rows[1].IsShorthand);
        Assert.IsTrue(grid.Columns[0].IsShorthand);
        Assert.IsTrue(grid.Columns[1].IsShorthand);
    }

    [TestMethod]
    public void ParseAllGrids_ShorthandSyntax_OffsetsBracketValues()
    {
        var grids = XamlGridParser.ParseAllGrids(ShorthandXaml);
        var grid = grids[0];

        // Entry offsets should bracket the actual value text in the XAML.
        Assert.AreEqual("*",    ShorthandXaml.Substring(grid.Rows[0].StartOffset, grid.Rows[0].EndOffset - grid.Rows[0].StartOffset));
        Assert.AreEqual("Auto", ShorthandXaml.Substring(grid.Rows[1].StartOffset, grid.Rows[1].EndOffset - grid.Rows[1].StartOffset));
        Assert.AreEqual("*",    ShorthandXaml.Substring(grid.Columns[0].StartOffset, grid.Columns[0].EndOffset - grid.Columns[0].StartOffset));
        Assert.AreEqual("200",  ShorthandXaml.Substring(grid.Columns[1].StartOffset, grid.Columns[1].EndOffset - grid.Columns[1].StartOffset));
    }

    [TestMethod]
    public void ParseAllGrids_ShorthandSyntax_ChildrenParsedCorrectly()
    {
        var grids = XamlGridParser.ParseAllGrids(ShorthandXaml);
        var grid = grids[0];

        Assert.AreEqual(2, grid.Children.Count);
        var childA = grid.Children.Find(c => c.Row == 0 && c.Column == 0);
        Assert.IsNotNull(childA);
        var childB = grid.Children.Find(c => c.Row == 1 && c.Column == 1);
        Assert.IsNotNull(childB);
    }
}
