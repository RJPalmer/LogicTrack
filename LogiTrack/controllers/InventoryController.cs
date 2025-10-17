using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;
using LogiTrack.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;

    private readonly IMemoryCache _cache;

    public InventoryController(LogiTrackContext context, IMemoryCache cache)
    {
        _cache = cache;
        _context = context;
    }

    /// <summary>
    /// Gets all inventory items.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInventoryItems()
    {
        // Store the result of the inventory query in memory for 30 seconds
        if (!_cache.TryGetValue("inventoryItems", out List<InventoryItem>? cachedItems))
        {
            cachedItems = await _context.InventoryItems.ToListAsync();
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromSeconds(30));
            // Save data in cache.
            _cache.Set("inventoryItems", cachedItems, cacheEntryOptions);
        }
        return Ok(cachedItems);
    }


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
    [Authorize(Roles = "Manager")]
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
    /// Updates an existing inventory item by ID.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> UpdateInventoryItem(int id, [FromBody] InventoryItem updatedItem)
    {
        if (updatedItem == null || (updatedItem.ItemId != 0 && updatedItem.ItemId != id))
        {
            return BadRequest();
        }

        var existingItem = await _context.InventoryItems.FindAsync(id);
        if (existingItem == null) return NotFound();

        existingItem.Name = updatedItem.Name;
        existingItem.Quantity = updatedItem.Quantity;
        existingItem.Location = updatedItem.Location;

        await _context.SaveChangesAsync();

        return Ok(existingItem);
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
