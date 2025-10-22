namespace LogiTrack.Controllers;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;
using LogiTrack.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private  LogiTrackContext _context;
    private readonly IMemoryCache _cache;

    public OrdersController(LogiTrackContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Gets all orders with caching.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders(CancellationToken cancellationToken)
    {
        const string cacheKey = "orders";

        try
        {
            var orders = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(5));
                return await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.OrderProducts)
                        .ThenInclude(op => op.InventoryItem)
                    .ToListAsync(cancellationToken);
            });
            
            return Ok(orders);
        }
        catch (System.Exception)
        {
            return StatusCode(500, "Internal server error");
        }

       
    }

    /// <summary>
    /// Gets a specific order by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderProducts)
                .ThenInclude(op => op.InventoryItem)
            .FirstOrDefaultAsync(o => o.OrderId == id, cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>
    /// Adds a new order.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddOrder([FromBody] Order newOrder, CancellationToken cancellationToken)
    {
        if (newOrder == null)
            return BadRequest("Invalid order data.");

        try
        {
            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync(cancellationToken);
            _cache.Remove("orders"); // invalidate cache
            return CreatedAtAction(nameof(GetOrder), new { id = newOrder.OrderId }, newOrder);
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes an order by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOrder(int id, CancellationToken cancellationToken)
    {
        var order = await _context.Orders.FindAsync(new object?[] { id }, cancellationToken);
        if (order == null)
            return NotFound();

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove("orders");
        return NoContent();
    }

    /// <summary>
    /// Updates an existing order.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOrder(int id, [FromBody] Order updatedOrder, CancellationToken cancellationToken)
    {
        if (id != updatedOrder.OrderId)
            return BadRequest("Order ID mismatch.");

        var existingOrder = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id, cancellationToken);
        if (existingOrder == null)
            return NotFound();

        existingOrder.CustomerName = updatedOrder.CustomerName;
        existingOrder.DatePlaced = updatedOrder.DatePlaced;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _cache.Remove("orders");
            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, $"Database update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds an item to an order.
    /// </summary>
    [HttpPost("{orderId:int}/items/{itemId:int}")]
    public async Task<IActionResult> AddItemToOrder(int orderId, int itemId, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.OrderProducts)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
        var item = await _context.InventoryItems.FindAsync(new object?[] { itemId }, cancellationToken);
        if (order == null || item == null)
            return NotFound();

        order.AddItem(item);
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove("orders");
        return NoContent();
    }

    /// <summary>
    /// Removes an item from an order.
    /// </summary>
    [HttpDelete("{orderId:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveItemFromOrder(int orderId, int itemId, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.OrderProducts)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
        var item = await _context.InventoryItems.FindAsync(new object?[] { itemId }, cancellationToken);
        if (order == null || item == null)
            return NotFound();

        order.RemoveItem(item);
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove("orders");
        return NoContent();
    }

    /// <summary>
    /// Gets all items in a specific order.
    /// </summary>
    [HttpGet("{orderId:int}/items")]
    public async Task<IActionResult> GetItemsInOrder(int orderId, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderProducts)
                .ThenInclude(op => op.InventoryItem)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

        return order == null ? NotFound() : Ok(order.OrderProducts);
    }
}