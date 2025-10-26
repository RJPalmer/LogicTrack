using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using LogiTrack.Models;
using Microsoft.AspNetCore.Identity;
using System.Net;

namespace LogiTrackTest;

public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

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
    }

    /// <summary>
    /// Tests that a user can log in and access a protected endpoint.
    /// Assumes a test user with email "testuser@example.com" and password "Test1234!" exists.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task LoginAndAccessProtectedEndpoint_ReturnsWhoAmI()
    {
        var client = _factory.CreateClient();

        //Create a test user directly via UserManager
        //todo: move to sepate test
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

        // Call protected endpoint with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var whoResp = await client.GetAsync("/api/protected/whoami");
        whoResp.EnsureSuccessStatusCode();
        var who = await whoResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(who);
        Assert.Equal("testuser@example.com", who["email"]);
    }

    /// <summary>
    /// Tests that login with invalid credentials returns Unauthorized.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task LoginWithInvalidCredentials_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        // Call login endpoint with invalid credentials
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { Email = "invalid@example.com", Password = "InvalidPassword!" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);
    }

    /// <summary>
    /// Tests that registering a new user returns success.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task RegisterNewUser_ReturnsSuccess()
    {
        var client = _factory.CreateClient();

        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "newuser@example.com",
            Password = "NewUser123!",
            ConfirmPassword = "NewUser123!"
        });

        registerResp.EnsureSuccessStatusCode();
    }
}
