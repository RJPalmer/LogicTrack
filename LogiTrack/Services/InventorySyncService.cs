using LogiTrack.Data;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Services
{
    /// <summary>
    /// Background service to periodically synchronize inventory data.
    /// </summary>
    public class InventorySyncService : BackgroundService
    {
        private readonly IServiceProvider _provider;

        public InventorySyncService(IServiceProvider provider)
        {
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _provider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();

                // Example: ensure data integrity or refresh cached values
                var count = await ctx.InventoryItems.CountAsync(stoppingToken);
                Console.WriteLine($"[InventorySyncService] Synced {count} inventory items at {DateTime.Now}.");

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}