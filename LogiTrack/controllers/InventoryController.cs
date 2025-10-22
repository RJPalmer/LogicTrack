using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;
using LogiTrack.Data;
using Microsoft.AspNetCore.Authorization;

namespace LogiTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;

    public InventoryController(LogiTrackContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all inventory items.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInventoryItems()
        => Ok(await _context.InventoryItems.ToListAsync());

    /// <summary>
    /// Gets a specific inventory item by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInventoryItem(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null) return NotFound();

        // Return explicit JSON result to ensure JSON payload
        return new JsonResult(item);
    }

    /// <summary>
    /// Adds a new inventory item.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddInventoryItem([FromBody] InventoryItem newItem)
    {
        try
        {
            if (newItem == null) return BadRequest();
            // Create a new entity instance so any client-supplied ItemId is not persisted.
            var itemToSave = new InventoryItem(newItem.Name, newItem.Quantity, newItem.Location, newItem.Price);
            _context.InventoryItems.Add(itemToSave);
            await _context.SaveChangesAsync();
            // Return the newly created entity (with generated ItemId)
            return CreatedAtAction(nameof(GetInventoryItem), new { id = itemToSave.ItemId }, itemToSave);
        }
        catch (System.Exception ex)
        {
            // Log the exception (logging not shown here)
            Console.WriteLine(ex);
            return StatusCode(500, "Internal server error");
            
        }
    }

    /// <summary>
    /// Deletes a specific inventory item by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> DeleteInventoryItem(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null) return NotFound();
        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Updates an existing inventory item.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateInventoryItem(int id, [FromBody] InventoryItem updatedItem)
    {
        if (updatedItem == null || id != updatedItem.ItemId)
            return BadRequest("Invalid inventory item data.");

        var existingItem = await _context.InventoryItems.FindAsync(id);
        if (existingItem == null) return NotFound();

        // Update the existing item's properties
        existingItem.Name = updatedItem.Name;
        existingItem.Quantity = updatedItem.Quantity;
        existingItem.Location = updatedItem.Location;
        existingItem.Price = updatedItem.Price;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Searches inventory items by name.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchInventoryItems([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Search term cannot be empty.");
        
        var results = await _context.InventoryItems
            .Where(item => item.Name.Contains(name))
            .ToListAsync();
        return Ok(results);
    }
}
