using Microsoft.VisualStudio.TestTools.UnitTesting;
using XAMLGridEditor.Core;

namespace XAMLGridEditor.Tests;

[TestClass]
public class GridManipulatorInsertTests
{
    // -----------------------------------------------------------------------
    // Shared fixture
    // -----------------------------------------------------------------------

    private const string BaseXaml = """
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

    private static GridInfo GetGrid(string xaml)
    {
        int offset = xaml.IndexOf("<Grid>");
        return XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
    }

    // -----------------------------------------------------------------------
    // InsertRow
    // -----------------------------------------------------------------------

    [TestMethod]
    public void InsertRow_BeforeIndex0_AddsDefinitionAndShiftsAll()
    {
        var grid = GetGrid(BaseXaml);
        string result = GridManipulator.InsertRow(BaseXaml, grid, beforeIndex: 0);

        var newGrid = GetGrid(result);
        Assert.AreEqual(3, newGrid.Rows.Count, "Should have 3 rows after insert.");

        // Both children should have their rows incremented.
        AssertAttributeValue(result, "TextBlock", "Grid.Row", 0, expectedValue: "1");
        AssertAttributeValue(result, "TextBlock", "Grid.Row", 1, expectedValue: "2");
    }

    [TestMethod]
    public void InsertRow_BeforeIndex1_OnlyShiftsChildrenAtOrAboveIndex()
    {
        var grid = GetGrid(BaseXaml);
        string result = GridManipulator.InsertRow(BaseXaml, grid, beforeIndex: 1);

        var newGrid = GetGrid(result);
        Assert.AreEqual(3, newGrid.Rows.Count);

        // Row 0 child stays at 0; row 1 child shifts to 2.
        AssertAttributeValue(result, "TextBlock", "Grid.Row", 0, expectedValue: "0");
        AssertAttributeValue(result, "TextBlock", "Grid.Row", 1, expectedValue: "2");
    }

    [TestMethod]
    public void InsertRow_AtEnd_AppendsDefinitionDoesNotShiftChildren()
    {
        var grid = GetGrid(BaseXaml);
        string result = GridManipulator.InsertRow(BaseXaml, grid, beforeIndex: 2);

        var newGrid = GetGrid(result);
        Assert.AreEqual(3, newGrid.Rows.Count, "Should have 3 rows after appending.");

        // Neither child should have moved.
        AssertAttributeValue(result, "TextBlock", "Grid.Row", 0, expectedValue: "0");
        AssertAttributeValue(result, "TextBlock", "Grid.Row", 1, expectedValue: "1");
    }

    [TestMethod]
    public void InsertRow_CustomHeight_UsedInDefinition()
    {
        var grid = GetGrid(BaseXaml);
        string result = GridManipulator.InsertRow(BaseXaml, grid, beforeIndex: 0, height: "100");
        StringAssert.Contains(result, "Height=\"100\"");
    }

    // -----------------------------------------------------------------------
    // InsertColumn
    // -----------------------------------------------------------------------

    [TestMethod]
    public void InsertColumn_BeforeIndex0_AddsDefinitionAndShiftsAll()
    {
        var grid = GetGrid(BaseXaml);
        string result = GridManipulator.InsertColumn(BaseXaml, grid, beforeIndex: 0);

        var newGrid = GetGrid(result);
        Assert.AreEqual(3, newGrid.Columns.Count, "Should have 3 columns after insert.");

        AssertAttributeValue(result, "TextBlock", "Grid.Column", 0, expectedValue: "1");
        AssertAttributeValue(result, "TextBlock", "Grid.Column", 1, expectedValue: "2");
    }

    [TestMethod]
    public void InsertColumn_BeforeIndex1_OnlyShiftsColumnsAtOrAboveIndex()
    {
        var grid = GetGrid(BaseXaml);
        string result = GridManipulator.InsertColumn(BaseXaml, grid, beforeIndex: 1);

        var newGrid = GetGrid(result);
        Assert.AreEqual(3, newGrid.Columns.Count);

        AssertAttributeValue(result, "TextBlock", "Grid.Column", 0, expectedValue: "0");
        AssertAttributeValue(result, "TextBlock", "Grid.Column", 1, expectedValue: "2");
    }

    // -----------------------------------------------------------------------
    // RowSpan handling on insert
    // -----------------------------------------------------------------------

    [TestMethod]
    public void InsertRow_SpanningElement_SpanIncreasedWhenInsertWithinSpan()
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
        string result = GridManipulator.InsertRow(xaml, grid, beforeIndex: 1);

        // Span should grow from 3 to 4.
        StringAssert.Contains(result, "Grid.RowSpan=\"4\"");
    }

    [TestMethod]
    public void InsertRow_SpanNotStraddled_SpanUnchanged()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Row="0" Grid.RowSpan="2" Text="Spanning"/>
                </Grid>
            </Window>
            """;

        int offset = xaml.IndexOf("<Grid>");
        var grid = XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
        // Insert at index 2 – outside the span (0..1).
        string result = GridManipulator.InsertRow(xaml, grid, beforeIndex: 2);

        StringAssert.Contains(result, "Grid.RowSpan=\"2\"");
    }

    // -----------------------------------------------------------------------
    // Implicit 0 on insert
    // -----------------------------------------------------------------------

    [TestMethod]
    public void InsertRow_ImplicitZero_AttributeAddedWhenInsertBeforeIndex0()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <TextBlock Text="No explicit row"/>
                </Grid>
            </Window>
            """;

        int offset = xaml.IndexOf("<Grid>");
        var grid = XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
        string result = GridManipulator.InsertRow(xaml, grid, beforeIndex: 0);

        // The TextBlock should now have Grid.Row="1" (implicit 0 became 1).
        StringAssert.Contains(result, "Grid.Row=\"1\"");
    }

    [TestMethod]
    public void InsertRow_ImplicitZero_NoAttributeAddedWhenInsertAfterIndex0()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <TextBlock Text="No explicit row"/>
                </Grid>
            </Window>
            """;

        int offset = xaml.IndexOf("<Grid>");
        var grid = XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
        // Insert at index 1 – element at implicit 0 should not move.
        string result = GridManipulator.InsertRow(xaml, grid, beforeIndex: 1);

        Assert.IsFalse(result.Contains("Grid.Row="),
            "No Grid.Row attribute should be added for an element that stays at row 0.");
    }

    // -----------------------------------------------------------------------
    // No definitions block (single implicit row/column)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void InsertRow_NoDefinitionsBlock_CreatesBlock()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid>
                    <TextBlock Text="Hello"/>
                </Grid>
            </Window>
            """;

        int offset = xaml.IndexOf("<Grid>");
        var grid = XamlGridParser.FindGridAtOffset(xaml, offset + 2)!;
        string result = GridManipulator.InsertRow(xaml, grid, beforeIndex: 0);

        StringAssert.Contains(result, "<Grid.RowDefinitions>");
        StringAssert.Contains(result, "<RowDefinition");
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static void AssertAttributeValue(string xaml, string elementName, string attrName,
        int childIndex, string expectedValue)
    {
        var grids = XamlGridParser.ParseAllGrids(xaml);
        Assert.IsTrue(grids.Count > 0);
        // Find matching children in document order.
        var matches = new System.Collections.Generic.List<GridChildInfo>();
        foreach (var g in grids)
            foreach (var c in g.Children)
                if (c.ElementName == elementName) matches.Add(c);

        // Sort by document order.
        matches.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

        Assert.IsTrue(childIndex < matches.Count, $"Expected at least {childIndex + 1} {elementName} children.");
        var child = matches[childIndex];

        // Read the attribute value directly from the XAML text.
        var (start, end) = GridChildAttributeUpdater.FindAttributeValueSpan(xaml, child.StartOffset, attrName);
        Assert.IsTrue(start >= 0, $"Attribute {attrName} not found on {elementName}[{childIndex}].");
        string actual = xaml.Substring(start, end - start);
        Assert.AreEqual(expectedValue, actual, $"{attrName} on {elementName}[{childIndex}]");
    }
}
