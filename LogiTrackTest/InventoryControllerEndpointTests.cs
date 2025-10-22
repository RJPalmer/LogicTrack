using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System;
using System.Threading.Tasks;

using Xunit;
using LogiTrack.Models;

namespace LogiTrackTest;

public class InventoryControllerEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InventoryControllerEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // Replace DB with in-memory Sqlite for integration testing
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<LogiTrack.Data.LogiTrackContext>));
                if (descriptor != null) services.Remove(descriptor);

                var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();

                services.AddDbContext<LogiTrack.Data.LogiTrackContext>(options =>
                    options.UseSqlite(connection));

                // Build service provider and create the schema
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogiTrack.Data.LogiTrackContext>();
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
            });
        });
    }

    [Fact]
    public async Task PostInventoryItem_ReturnsCreated_WithLocationAndGeneratedId()
    {
        var client = _factory.CreateClient();

        string token = await AuthorizeClient(client);
        // Set Bearer token for authorization
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    
        var newItem = new InventoryItem("Endpoint Widget", 7, "Z1", 4.99);

        var resp = await client.PostAsJsonAsync("/api/inventory", newItem);

        resp.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);

        // Location header should point to the resource
        Assert.True(resp.Headers.Location != null, "Location header should be set");

        // Response body should contain the created object with generated ItemId
        var created = await resp.Content.ReadFromJsonAsync<InventoryItem>();
        Assert.NotNull(created);
        Assert.True(created.ItemId > 0, "Generated ItemId should be greater than 0");
        Assert.Equal(newItem.Name, created.Name);

        // Verify GET /api/inventory/{id} returns the same item
        var getResp = await client.GetAsync(resp.Headers.Location);
        getResp.EnsureSuccessStatusCode();
        var fetched = await getResp.Content.ReadFromJsonAsync<InventoryItem>();
        Assert.NotNull(fetched);
        Assert.Equal(created.ItemId, fetched.ItemId);
        Assert.Equal(created.Name, fetched.Name);
        Assert.Equal(created.Quantity, fetched.Quantity);
        Assert.Equal(created.Location, fetched.Location);
    }

    /// <summary>
    /// Helper to authorize client and get JWT token
    /// </summary>
    /// <param name="client"></param>
    /// <returns>JWT token string</returns>
    private async Task<string> AuthorizeClient(HttpClient client)
    {
        // Create a test user directly via UserManager
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var testUser = new ApplicationUser { UserName = "testuser@example.com", Email = "testuser@example.com", EmailConfirmed = true };
            var result = await userManager.CreateAsync(testUser, "Test1234!");
            Assert.True(result.Succeeded, string.Join(';', result.Errors.Select(e => e.Code)));
        }

        // Call login endpoint
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { Email = "testuser@example.com", Password = "Test1234!" });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(loginBody);
        Assert.True(loginBody.ContainsKey("token"));
        var token = loginBody["token"];
        return token;
    }
}
    