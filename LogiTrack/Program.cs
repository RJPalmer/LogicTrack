using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Register LogiTrackContext with DI so other services/controllers can get it.
var connectionString = builder.Configuration.GetConnectionString("LogiTrack")
    ?? "Data Source=logitrack.db";
builder.Services.AddDbContext<LogiTrackContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
// --- Quick EF Core sanity test at startup ---
try
{
    Console.WriteLine("Starting EF Core sanity check (using DI-scoped DbContext)...");
    using (var scope = app.Services.CreateScope())
    {
        var ctx = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();

        // Recreate a clean database for the sanity test to avoid leftover bad data
        // (WARNING: this deletes the local database file 'logitrack.db').
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        // Seed test data if none exists.
        if (!ctx.InventoryItems.Any())
        {
            var seedItems = new List<InventoryItem>
            {
                new InventoryItem("Widget", 10, "A1"),
                new InventoryItem("Gadget", 5, "B2"),
                new InventoryItem("Bolt", 100, "C3"),
            };

            ctx.InventoryItems.AddRange(seedItems);
            ctx.SaveChanges();
            Console.WriteLine("Seeded InventoryItems with sample data.");
        }

        // Query and print items.
        var items = ctx.InventoryItems.AsNoTracking().ToList();
        Console.WriteLine($"InventoryItems ({items.Count}):");
        foreach (var it in items)
        {
            Console.WriteLine(it.ToString());
        }
    }
    Console.WriteLine("EF Core sanity check completed successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"EF Core sanity check failed: {ex}");
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
