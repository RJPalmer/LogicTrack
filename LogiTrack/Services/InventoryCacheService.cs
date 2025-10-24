using System.Text.Json;
using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace LogiTrack.Services
{
    /// <summary>
    /// Service to manage inventory caching using distributed cache (e.g., Redis).
    /// </summary>
    public class InventoryCacheService
    {
        /// <summary>
        /// Database context for accessing inventory data.
        /// </summary>
        private readonly LogiTrackContext _context;

        /// <summary>
        /// Distributed cache for storing inventory data.
        /// </summary>
        private readonly IDistributedCache _cache;

        /// <summary>
        /// Cache key for inventory items.
        /// </summary>
        private const string CacheKey = "inventory_cache_v1";

        /// <summary>
        /// Cache entry options.
        /// </summary>
        private readonly DistributedCacheEntryOptions _cacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            SlidingExpiration = TimeSpan.FromMinutes(10)
        };

        public InventoryCacheService(LogiTrackContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <summary>
        /// Retrieves inventory items from cache or database.
        /// </summary>
        public async Task<List<InventoryItem>> GetInventoryAsync()
        {
            var cachedData = await _cache.GetStringAsync(CacheKey);
            if (!string.IsNullOrEmpty(cachedData))
            {
                Console.WriteLine("[Redis] Returning cached inventory data.");
                return JsonSerializer.Deserialize<List<InventoryItem>>(cachedData)!;
            }

            Console.WriteLine("[Redis] Cache miss — fetching from database.");
            var items = await _context.InventoryItems.AsNoTracking().ToListAsync();
            await SetInventoryCacheAsync(items);
            return items;
        }

        /// <summary>
        /// Adds a new inventory item and updates the cache.
        /// </summary>
        public async Task<InventoryItem> AddInventoryItemAsync(InventoryItem item)
        {
            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            await RefreshInventoryCacheAsync();
            return item;
        }

        /// <summary>
        /// Updates the cached data explicitly (e.g., after changes).
        /// </summary>
        public async Task RefreshInventoryCacheAsync()
        {
            var items = await _context.InventoryItems.AsNoTracking().ToListAsync();
            await SetInventoryCacheAsync(items);
        }

        private async Task SetInventoryCacheAsync(List<InventoryItem> items)
        {
            var jsonData = JsonSerializer.Serialize(items);
            await _cache.SetStringAsync(CacheKey, jsonData, _cacheOptions);
            Console.WriteLine($"[Redis] Cache updated with {items.Count} items at {DateTime.Now}.");
        }
    }
}