using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XAMLGridEditor.Core;

namespace XAMLGridEditor.Tests;

[TestClass]
public class GridManipulatorRemoveTests
{
    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    private const string ThreeRowXaml = """
        <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="100"/>
                </Grid.RowDefinitions>
                <TextBlock Grid.Row="0" Text="R0"/>
                <TextBlock Grid.Row="1" Text="R1"/>
                <TextBlock Grid.Row="2" Text="R2"/>
            </Grid>
        </Window>
        """;

    private const string ThreeColXaml = """
        <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="100"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="C0"/>
                <TextBlock Grid.Column="1" Text="C1"/>
                <TextBlock Grid.Column="2" Text="C2"/>
            </Grid>
        </Window>
        """;

    private static GridInfo GetGrid(string xaml)
    {
        int offset = xaml.IndexOf("<Grid>");
        return XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
    }

    // -----------------------------------------------------------------------
    // RemoveRow – basic shifting
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RemoveRow_Index0_ShiftsRemainingRows()
    {
        var grid = GetGrid(ThreeRowXaml);
        string result = GridManipulator.RemoveRow(ThreeRowXaml, grid, removeIndex: 0, out var warnings);

        var newGrid = GetGrid(result);
        Assert.AreEqual(2, newGrid.Rows.Count, "Should have 2 rows after remove.");

        // R1 should move to row 0, R2 to row 1.
        AssertContainsAttribute(result, "Grid.Row=\"0\"");
        AssertContainsAttribute(result, "Grid.Row=\"1\"");
        Assert.IsFalse(result.Contains("Grid.Row=\"2\""), "Row 2 should no longer exist.");
    }

    [TestMethod]
    public void RemoveRow_MiddleIndex_ShiftsOnlyHigherRows()
    {
        var grid = GetGrid(ThreeRowXaml);
        string result = GridManipulator.RemoveRow(ThreeRowXaml, grid, removeIndex: 1, out _);

        // R0 stays at 0, R2 moves to 1.
        AssertContainsAttribute(result, "Grid.Row=\"0\"");
        AssertContainsAttribute(result, "Grid.Row=\"1\"");
        Assert.IsFalse(result.Contains("Grid.Row=\"2\""));
    }

    [TestMethod]
    public void RemoveRow_LastIndex_DefinitionRemovedChildrenUnchanged()
    {
        var grid = GetGrid(ThreeRowXaml);
        string result = GridManipulator.RemoveRow(ThreeRowXaml, grid, removeIndex: 2, out _);

        var newGrid = GetGrid(result);
        Assert.AreEqual(2, newGrid.Rows.Count);
        // R0 and R1 unchanged.
        AssertContainsAttribute(result, "Grid.Row=\"0\"");
        AssertContainsAttribute(result, "Grid.Row=\"1\"");
    }

    // -----------------------------------------------------------------------
    // RemoveColumn – basic shifting
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RemoveColumn_Index0_ShiftsRemainingColumns()
    {
        var grid = GetGrid(ThreeColXaml);
        string result = GridManipulator.RemoveColumn(ThreeColXaml, grid, removeIndex: 0, out _);

        var newGrid = GetGrid(result);
        Assert.AreEqual(2, newGrid.Columns.Count);

        AssertContainsAttribute(result, "Grid.Column=\"0\"");
        AssertContainsAttribute(result, "Grid.Column=\"1\"");
        Assert.IsFalse(result.Contains("Grid.Column=\"2\""));
    }

    // -----------------------------------------------------------------------
    // Guard: cannot remove the last definition
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RemoveRow_OnlyOneRow_ReturnsWarningAndUnchangedXaml()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Row="0" Text="Only row"/>
                </Grid>
            </Window>
            """;

        var grid = GetGrid(xaml);
        string result = GridManipulator.RemoveRow(xaml, grid, removeIndex: 0, out var warnings);

        Assert.AreEqual(xaml, result, "XAML should be unchanged when trying to remove the only row.");
        Assert.IsTrue(warnings.Count > 0, "Should warn about removing last row.");
    }

    [TestMethod]
    public void RemoveColumn_OnlyOneColumn_ReturnsWarningAndUnchangedXaml()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="Only col"/>
                </Grid>
            </Window>
            """;

        var grid = GetGrid(xaml);
        string result = GridManipulator.RemoveColumn(xaml, grid, removeIndex: 0, out var warnings);

        Assert.AreEqual(xaml, result);
        Assert.IsTrue(warnings.Count > 0);
    }

    // -----------------------------------------------------------------------
    // Span clamping on remove
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RemoveRow_SpanCoversRemovedRow_SpanClamped()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Row="0" Grid.RowSpan="3" Text="Spanning"/>
                </Grid>
            </Window>
            """;

        int offset = xaml.IndexOf("<Grid>");
        var grid = XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
        string result = GridManipulator.RemoveRow(xaml, grid, removeIndex: 1, out var warnings);

        // Span should be clamped from 3 to 2.
        StringAssert.Contains(result, "Grid.RowSpan=\"2\"");
        Assert.IsTrue(warnings.Count > 0, "Should warn about span clamping.");
    }

    // -----------------------------------------------------------------------
    // Warnings for element on removed row
    // -----------------------------------------------------------------------

    [TestMethod]
    public void RemoveRow_ElementOnRemovedRow_EmitsWarning()
    {
        var grid = GetGrid(ThreeRowXaml);
        _ = GridManipulator.RemoveRow(ThreeRowXaml, grid, removeIndex: 1, out var warnings);

        Assert.IsTrue(warnings.Count > 0, "Should warn that R1 element is on the removed row.");
    }

    // -----------------------------------------------------------------------
    // Shorthand attribute syntax (e.g. RowDefinitions="Auto, *, 100")
    // -----------------------------------------------------------------------

    private const string ShorthandThreeRow = """
        <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid RowDefinitions="Auto, *, 100">
                <TextBlock Grid.Row="0" Text="R0"/>
                <TextBlock Grid.Row="1" Text="R1"/>
                <TextBlock Grid.Row="2" Text="R2"/>
            </Grid>
        </Window>
        """;

    private static GridInfo GetShorthandGrid(string xaml)
    {
        int offset = xaml.IndexOf("<Grid ");
        return XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
    }

    [TestMethod]
    public void RemoveRow_ShorthandSyntax_RemovesValueFromAttribute()
    {
        var grid = GetShorthandGrid(ShorthandThreeRow);
        string result = GridManipulator.RemoveRow(ShorthandThreeRow, grid, removeIndex: 1, out _);

        var newGrid = GetShorthandGrid(result);
        Assert.AreEqual(2, newGrid.Rows.Count);
        Assert.IsTrue(newGrid.HasShorthandRowDefinitions);
        Assert.AreEqual("Auto", newGrid.Rows[0].Size);
        Assert.AreEqual("100",  newGrid.Rows[1].Size);
    }

    [TestMethod]
    public void RemoveRow_ShorthandSyntax_ShiftsChildAttributes()
    {
        var grid = GetShorthandGrid(ShorthandThreeRow);
        string result = GridManipulator.RemoveRow(ShorthandThreeRow, grid, removeIndex: 0, out _);

        // R1 moves to row 0, R2 to row 1.
        AssertContainsAttribute(result, "Grid.Row=\"0\"");
        AssertContainsAttribute(result, "Grid.Row=\"1\"");
    }

    [TestMethod]
    public void RemoveRow_ShorthandOnlyOneRow_ReturnsWarning()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid RowDefinitions="*">
                    <TextBlock Grid.Row="0" Text="Only"/>
                </Grid>
            </Window>
            """;

        var grid = GetShorthandGrid(xaml);
        string result = GridManipulator.RemoveRow(xaml, grid, removeIndex: 0, out var warnings);

        Assert.AreEqual(xaml, result);
        Assert.IsTrue(warnings.Count > 0);
    }

    // -----------------------------------------------------------------------
    // Multi-dialect: MAUI namespace
    // -----------------------------------------------------------------------

    [TestMethod]
    public void InsertRow_MauiDialect_WorksCorrectly()
    {
        const string xaml = """
            <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <Label Grid.Row="0" Text="MAUI"/>
                    <Label Grid.Row="1" Text="Row1"/>
                </Grid>
            </ContentPage>
            """;

        int offset = xaml.IndexOf("<Grid>");
        var grid = XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
        Assert.IsNotNull(grid, "Should parse MAUI XAML grid.");

        string result = GridManipulator.InsertRow(xaml, grid, beforeIndex: 0);
        var newGrid = XamlGridParser.ParseAllGrids(result)[0];
        Assert.AreEqual(3, newGrid.Rows.Count);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void AssertContainsAttribute(string xaml, string expected)
        => StringAssert.Contains(xaml, expected);
}
