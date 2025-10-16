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
        if (newItem == null) return BadRequest();
        // Create a new entity instance so any client-supplied ItemId is not persisted.
        var itemToSave = new InventoryItem(newItem.Name, newItem.Quantity, newItem.Location);
        _context.InventoryItems.Add(itemToSave);
        await _context.SaveChangesAsync();
        // Return the newly created entity (with generated ItemId)
        return CreatedAtAction(nameof(GetInventoryItem), new { id = itemToSave.ItemId }, itemToSave);
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
}
