
namespace LogiTrack.Controllers;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;
using LogiTrack.Data;
using Microsoft.AspNetCore.Authorization;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    LogiTrackContext _context;

    /// <summary>
    /// Constructor for OrdersController.
    /// </summary>
    /// <param name="context"></param>
    public OrdersController(LogiTrackContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all orders.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _context.Orders.ToListAsync();
        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return NotFound();
        return Ok(order);
    }

    /// <summary>
    /// Adds a new order.
    /// </summary>
    /// <param name="newOrder"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> AddOrder([FromBody] Order newOrder)
    {
        if (newOrder == null)
            return BadRequest();

        _context.Orders.Add(newOrder);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOrder), new { id = newOrder.OrderId }, newOrder);
    }

    /// <summary>
    /// Deletes an order by ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return NotFound();

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Updates an existing order.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="updatedOrder"></param>
    /// <returns></returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOrder(int id, [FromBody] Order updatedOrder)
    {
        if (id != updatedOrder.OrderId)
            return BadRequest();

        var existingOrder = await _context.Orders.FindAsync(id);
        if (existingOrder == null)
            return NotFound();

        existingOrder.CustomerName = updatedOrder.CustomerName;
        existingOrder.DatePlaced = updatedOrder.DatePlaced;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Adds an item to an order.
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="itemId"></param>
    /// <returns></returns>
    [HttpPost("{orderId:int}/items/{itemId:int}")]
    public async Task<IActionResult> AddItemToOrder(int orderId, int itemId)
    {
        var order = await _context.Orders.Include(o => o.OrderProducts).FirstOrDefaultAsync(o => o.OrderId == orderId);
        var item = await _context.InventoryItems.FindAsync(itemId);
        if (order == null || item == null)
            return NotFound();

        order.AddItem(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Removes an item from an order.
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="itemId"></param>
    /// <returns></returns>
    [HttpDelete("{orderId:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveItemFromOrder(int orderId, int itemId)
    {
        var order = await _context.Orders.Include(o => o.OrderProducts).FirstOrDefaultAsync(o => o.OrderId == orderId);
        var item = await _context.InventoryItems.FindAsync(itemId);
        if (order == null || item == null)
            return NotFound();

        order.RemoveItem(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Gets all items in a specific order.
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    [HttpGet("{orderId:int}/items")]
    public async Task<IActionResult> GetItemsInOrder(int orderId)
    {
        var order = await _context.Orders.Include(o => o.OrderProducts).FirstOrDefaultAsync(o => o.OrderId == orderId);
        if (order == null)
            return NotFound();

        return Ok(order.OrderProducts);
    }
}