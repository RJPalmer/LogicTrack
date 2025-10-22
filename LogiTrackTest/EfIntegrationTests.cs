using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using LogiTrack.Data;
using LogiTrack.Models;

namespace LogiTrackTest;

public class EfIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LogiTrackContext> _options;

    public EfIntegrationTests()
    {
        // Create in-memory SQLite with shared cache so connection stays alive
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LogiTrackContext>()
            .UseSqlite(_connection)
            .Options;
    }

    [Fact]
    public void SanityCheck_CreateSeedAndReadInventoryItems()
    {
        // Capture console output for assertion
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            using (var ctx = new LogiTrackContext(_options))
            {
                // Ensure clean schema
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                // Seed data
                var seedItems = new List<InventoryItem>
                {
                    new InventoryItem("TestWidget", 1, "T1", 9.99),
                    new InventoryItem("TestGadget", 2, "T2", 19.99),
                };

                ctx.InventoryItems.AddRange(seedItems);
                ctx.SaveChanges();

                // Query and print
                var items = ctx.InventoryItems.AsNoTracking().ToList();
                Console.WriteLine($"InventoryItems ({items.Count}):");
                foreach (var it in items)
                {
                    Console.WriteLine(it.ToString());
                }
            }

            var output = sw.ToString();
            Assert.Contains("InventoryItems (2)", output);
            Assert.Contains("Name: TestWidget", output);
            Assert.Contains("Name: TestGadget", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
