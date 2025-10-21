
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
}