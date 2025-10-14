using System;
using System.IO;
using Xunit;
using LogiTrack.Models;

namespace LogiTrackTest;

public class InventoryItemTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var item = new InventoryItem(1, "Widget", 10, "A1");

        Assert.Equal(1, item.ItemId);
        Assert.Equal("Widget", item.Name);
        Assert.Equal(10, item.Quantity);
        Assert.Equal("A1", item.Location);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var item = new InventoryItem(2, "Gadget", 5, "B2");
        var s = item.ToString();

        Assert.Contains("ItemId: 2", s);
        Assert.Contains("Name: Gadget", s);
        Assert.Contains("Quantity: 5", s);
        Assert.Contains("Location: B2", s);
    }

    [Fact]
    public void UpdateQuantity_ChangesQuantity()
    {
        var item = new InventoryItem(3, "Part", 0, "C3");
        item.UpdateQuantity(42);
        Assert.Equal(42, item.Quantity);
    }

    [Fact]
    public void UpdateLocation_ChangesLocation()
    {
        var item = new InventoryItem(4, "Bolt", 100, "D4");
        item.UpdateLocation("Z9");
        Assert.Equal("Z9", item.Location);
    }

    [Fact]
    public void DisplayInfo_WritesExpectedOutput()
    {
        var item = new InventoryItem(6, "Pallet Jack", 12, "Warehouse A");

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            item.DisplayInfo();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString().Trim();
        Assert.Equal("Item: Pallet Jack | Quantity: 12 | Location: Warehouse A", output);
    }
}
