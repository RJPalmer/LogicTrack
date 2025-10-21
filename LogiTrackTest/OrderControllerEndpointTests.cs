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
    public async Task GetOrders_ReturnsOkResult()
    {
        // Arrange
        var client = _factory.CreateClient();

        string token = await AuthorizeClient(client);

        // Call protected endpoint with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);


        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
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