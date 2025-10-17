using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using LogiTrack.Models;
using Microsoft.AspNetCore.Identity;

namespace LogiTrackTest;

/// <summary>
/// Integration tests for authentication and protected endpoints.
/// </summary>
public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private string _token = null!;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<LogiTrack.Data.LogiTrackContext>));
                if (descriptor != null) services.Remove(descriptor);

                var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();

                services.AddDbContext<LogiTrack.Data.LogiTrackContext>(options =>
                    options.UseSqlite(connection));

                // Ensure Identity and UserManager are available and database is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LogiTrack.Data.LogiTrackContext>();
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
            });
        });

        InitializeClientAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeClientAsync()
    {
        _client = _factory.CreateClient();

        // Register the test user with a Manager role
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new { Email = "testuser@example.com", Password = "Test1234!", Roles = new[] { "Manager" }     });
        registerResponse.EnsureSuccessStatusCode();

        // Login the test user
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new { Email = "testuser@example.com", Password = "Test1234!" });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(loginBody);
        Assert.True(loginBody.ContainsKey("token"), "Login response should contain a Token");
        _token = loginBody["token"];

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    [Fact]
    public async Task LoginAndAccessProtectedEndpoint_ReturnsWhoAmI()
    {
        var whoResp = await _client.GetAsync("/api/protected/whoami");
        whoResp.EnsureSuccessStatusCode();
        var who = await whoResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(who);
        Assert.Equal("testuser@example.com", who["email"]);
    }
}
