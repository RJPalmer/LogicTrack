using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using LogiTrack.Models;
using System.Net.Http.Headers;

namespace LogiTrackTest;

public class InventoryControllerEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private  HttpClient _client;
    private static readonly string[] value = new[] { "Manager" };

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

        //_client = _factory.CreateClient();
        InitializeClientAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeClientAsync()
    {
        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new { Email = "testuser@example.com", Password = "Test1234!", Roles = new[] { "Manager" } });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(registerBody);
        Assert.True(registerBody.ContainsKey("message"), "Register response should contain a Message");
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = "testuser@example.com", Password = "Test1234!" });
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(loginBody);
        Assert.True(loginBody.ContainsKey("token"), "Login response should contain a Token");
        var token = loginBody["token"];
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task PostInventoryItem_ReturnsCreated_WithLocationAndGeneratedId()
    {
        var newItem = new InventoryItem("Endpoint Widget", 7, "Z1");

        var resp = await _client.PostAsJsonAsync("/api/inventory", newItem);
        if(!resp.IsSuccessStatusCode){
            var errorContent = await resp.Content.ReadAsStringAsync();
            Assert.Fail($"Request failed with status {resp.StatusCode}: {errorContent}");
        }
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
        var getResp = await _client.GetAsync(resp.Headers.Location);
        getResp.EnsureSuccessStatusCode();
        var fetched = await getResp.Content.ReadFromJsonAsync<InventoryItem>();
        Assert.NotNull(fetched);
        Assert.Equal(created.ItemId, fetched.ItemId);
        Assert.Equal(created.Name, fetched.Name);
        Assert.Equal(created.Quantity, fetched.Quantity);
        Assert.Equal(created.Location, fetched.Location);
    }
}
