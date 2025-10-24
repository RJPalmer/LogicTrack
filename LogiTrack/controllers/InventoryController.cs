using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogiTrack.Models;
using LogiTrack.Data;
using LogiTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Storage;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Distributed;
namespace LogiTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;

    //property for redis cache
    //private readonly IDatabaseCacheService _inventoryCache;

    private readonly IDistributedCache _redisDb;

    // private readonly InventoryCacheService _inventoryCache;

    /// <summary>
    /// Constructor to initialize the InventoryController with the cache service.
    /// </summary>
    // public InventoryController(InventoryCacheService inventoryCache)
    // {
    //     _inventoryCache = inventoryCache;
    // }


    /// <summary>
    /// Constructor to initialize the InventoryController with the database context and Redis database.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="redisDb"></param>
    [Obsolete("Use InventoryController(LogiTrackContext context, IConnectionMultiplexer redisDb) instead.")]
    public InventoryController(LogiTrackContext context, IDistributedCache redisDb)
    {
        _context = context;
        _redisDb = redisDb;
    }

    /// <summary>
    /// Gets all inventory items.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInventoryItems()
    {
        //retrieve from cache or database
        var cachedItems = await _redisDb.GetStringAsync("inventory_items");
        if (!string.IsNullOrEmpty(cachedItems))
        {
            var itemsFromCache = System.Text.Json.JsonSerializer.Deserialize<List<InventoryItem>>(cachedItems);
            await _redisDb.RefreshAsync("inventory_items");
            return Ok(itemsFromCache);
        }
        var items = await _context.InventoryItems.AsNoTracking().ToListAsync();
        await _redisDb.SetStringAsync("inventory_items", System.Text.Json.JsonSerializer.Serialize(items));
        return Ok(items);
    }

    /// <summary>
    /// Gets a specific inventory item by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInventoryItem(int id)
    {
        //retrieve from cache or database
        var cachedItem = await _redisDb.GetStringAsync($"inventory_item_{id}");
        if (string.IsNullOrEmpty(cachedItem))
        {
            //retrieve from database
            var itemFromDB = await _context.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.ItemId == id);
            if (itemFromDB == null) return NotFound();
            //store in cache
            await _redisDb.SetStringAsync($"inventory_item_{id}", System.Text.Json.JsonSerializer.Serialize(itemFromDB));
            return Ok(itemFromDB);
        }

        var item = System.Text.Json.JsonSerializer.Deserialize<InventoryItem>(cachedItem);

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
        var itemToSave = new InventoryItem(newItem.Name, newItem.Quantity, newItem.Location, newItem.Price);
        try
        {
            //validate itemToSave
            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(itemToSave);
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(itemToSave, validationContext, validateAllProperties: true);

            //add to database
            _context.InventoryItems.Add(itemToSave);
            await _context.SaveChangesAsync();

            //update cache
            await _redisDb.SetStringAsync($"inventory_item_{itemToSave.ItemId}", System.Text.Json.JsonSerializer.Serialize(itemToSave));
            await _redisDb.SetStringAsync("inventory_items", System.Text.Json.JsonSerializer.Serialize(new List<InventoryItem> { itemToSave }));

            await _redisDb.RefreshAsync("inventory_items");

        }
        catch (System.Exception ex)
        {
            return BadRequest($"Validation or database error: {ex.Message}");
        }
        // Return the newly created entity (with generated ItemId)
        itemToSave = await _context.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.Name == itemToSave.Name);
        return CreatedAtAction(nameof(GetInventoryItem), new { id = itemToSave.ItemId }, itemToSave);
    }


    /// <summary>
    /// Deletes a specific inventory item by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> DeleteInventoryItem(int id)
    {
        //Check if item exists in cache or database
        var item = await _redisDb.GetStringAsync($"inventory_item_{id}");
        if (string.IsNullOrEmpty(item))
        {
            //if not in cache check database
            var dbItem = await _context.InventoryItems.FindAsync(id);

            //if not in database return not found
            if (dbItem == null) return NotFound();

            //remove from database
            _context.InventoryItems.Remove(dbItem);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        await _redisDb.RemoveAsync($"inventory_item_{id}");
        await _context.InventoryItems.Where(i => i.ItemId == id).ExecuteDeleteAsync();
        //update cache
        await _redisDb.SetStringAsync("inventory_items", System.Text.Json.JsonSerializer.Serialize(new List<InventoryItem>()));
        await _context.SaveChangesAsync();
        await _redisDb.RefreshAsync("inventory_items");
        return NoContent();
    }

    /// <summary>
    /// Searches inventory items by name or location.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchInventoryItems([FromQuery]string searchTerm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return BadRequest("Invalid search term.");
    
            // Check cache first
            var cachedItems = await _redisDb.GetStringAsync($"inventory_items_search_{searchTerm}");
            if (!string.IsNullOrEmpty(cachedItems))
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<List<InventoryItem>>(cachedItems);
                return Ok(items);
            }
    
            // If not in cache, query database
            var itemsFromDb = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => i.Name.Contains(searchTerm) || i.Location.Contains(searchTerm))
                .ToListAsync();
    
            if (itemsFromDb == null || itemsFromDb.Count == 0) return NotFound();
    
            // Store result in cache
            await _redisDb.SetStringAsync($"inventory_items_search_{searchTerm}", System.Text.Json.JsonSerializer.Serialize(itemsFromDb));
    
            return Ok(itemsFromDb);
        }
        catch (System.Exception)
        {
            return StatusCode(500, "An error occurred while searching inventory items.");
            
        }
    }
}
