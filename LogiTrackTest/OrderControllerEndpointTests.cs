namespace LogiTrackTest;

using Xunit;
using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Json;
using System.Net.Http.Headers;


/// <summary>
/// Tests for the OrderController.
/// </summary>
public class OrderControllerEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OrderControllerEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
       {
           // Replace DB with in-memory Sqlite for integration testing
           builder.ConfigureServices(services =>
           {
               var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<LogiTrack.Data.LogiTrackContext>));
               if (descriptor != null) services.Remove(descriptor);

               var connection = new SqliteConnection("DataSource=:memory:");
               connection.Open();

               services.AddSingleton(connection);
               services.AddDbContext<LogiTrack.Data.LogiTrackContext>(options =>
                   options.UseSqlite(connection));

               //add missing dependency for OrdersController
               services.AddMemoryCache();
                
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
    public async Task GetOrders_ReturnsOkResult()
    {
        // Arrange
        var client = _factory.CreateClient();

        string token = await AuthorizeClient(client);

        //create cancellation token
        CancellationTokenSource cts = new CancellationTokenSource();
        CancellationToken cancellationToken = cts.Token;


        // Call protected endpoint with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);


        // Act
        var response = await client.GetAsync("/api/orders", cancellationToken);

        // Assert
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Assert.Fail($"Request to /api/orders failed: {ex.Message}");
            //todo implement output logging
        }
        var orders = await response.Content.ReadFromJsonAsync<List<Order>>();
        Assert.NotNull(orders);
        Assert.Empty(orders);   // Should be empty initially

        // Add an order to verify retrieval
        var newOrder = new Order
        {
            CustomerName = "Alice Johnson",
            DatePlaced = DateTime.UtcNow
        };
        var postResp = await client.PostAsJsonAsync("/api/orders", newOrder);
        postResp.EnsureSuccessStatusCode();

        // Act again
        var response2 = await client.GetAsync("/api/orders");
        response2.EnsureSuccessStatusCode();
        var orders2 = await response2.Content.ReadFromJsonAsync<List<Order>>();
        Assert.NotNull(orders2);
        Assert.Single(orders2);
    }

    /// <summary>
    /// Tests getting a specific order by ID.
    /// </summary>
    [Fact]
    public async Task GetOrder_ReturnsOkResult()
    {
        // Arrange
        var client = _factory.CreateClient();

        string token = await AuthorizeClient(client);

        // Call protected endpoint with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        //add an order to fetch
        var newOrder = new Order
        {
            CustomerName = "Jane Smith",
            DatePlaced = DateTime.UtcNow
        };
        var postResp = await client.PostAsJsonAsync("/api/orders", newOrder);
        postResp.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/api/orders/1");

        // Assert
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);
    }

    /// <summary>
    /// Tests adding a new order.
    /// </summary>
    [Fact]
    public async Task PostOrder_AddsNewOrder_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();

        string token = await AuthorizeClient(client);

        // Call protected endpoint with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newOrder = new Order
        {
            CustomerName = "John Doe",
            DatePlaced = DateTime.UtcNow
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", newOrder);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var createdOrder = await response.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(createdOrder);
        Assert.True(createdOrder.OrderId > 0);
        Assert.Equal(newOrder.CustomerName, createdOrder.CustomerName);
    }

    /// <summary>
    /// Tests adding a null order returns BadRequest.
    /// </summary>
    [Fact]
    public async Task PostOrder_NullOrder_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        string token = await AuthorizeClient(client);

        // Call protected endpoint with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", (Order?)null);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    ///<summary>
    /// Tests removing an order
    /// </summary>
    [Fact]
    public async Task DeleteOrder_RemovesOrder_ReturnsNoContent()
    {
        // Arrange
        var client = _factory.CreateClient();
        string token = await AuthorizeClient(client);

        // Call protected endpoint with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        //add an order to delete
        var newOrder = new Order
        {
            CustomerName = "Mark Spencer",
            DatePlaced = DateTime.UtcNow
        };

        var postResp = await client.PostAsJsonAsync("/api/orders", newOrder);
        postResp.EnsureSuccessStatusCode();

        var createdOrder = await postResp.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(createdOrder);
        Assert.True(createdOrder.OrderId > 0);

        // Act
        var response = await client.DeleteAsync($"/api/orders/{createdOrder.OrderId}");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Helper to authorize client and get JWT token.
    /// Ensures a consistent test user setup and valid login response.
    /// </summary>
    /// <param name="client">The test HttpClient instance.</param>
    /// <returns>JWT token string</returns>
    private async Task<string> AuthorizeClient(HttpClient client)
    {
        const string testEmail = "testuser@example.com";
        const string testPassword = "Test1234!";

        // Ensure test user exists in the in-memory identity database
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var existingUser = await userManager.FindByEmailAsync(testEmail);

            if (existingUser == null)
            {
                var newUser = new ApplicationUser
                {
                    UserName = testEmail,
                    Email = testEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(newUser, testPassword);
                Assert.True(createResult.Succeeded, $"User creation failed: {string.Join(';', createResult.Errors.Select(e => e.Description))}");
            }
        }

        // Attempt login to get a valid JWT token
        var loginRequest = new { Email = testEmail, Password = testPassword };
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert login success
        Assert.True(loginResponse.IsSuccessStatusCode, $"Login failed with status {loginResponse.StatusCode}");
        var responseBody = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(responseBody);
        Assert.True(responseBody.ContainsKey("token"), "Login response does not contain 'token' key.");

        string token = responseBody["token"];
        Assert.False(string.IsNullOrWhiteSpace(token), "Received empty JWT token.");

        return token;
    }

}