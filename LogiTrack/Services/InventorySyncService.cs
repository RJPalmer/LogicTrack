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

        /// <summary>
        /// Executes the background synchronization task.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Periodically synchronize inventory data every 10 minutes
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _provider.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();

                // Example: ensure data integrity or refresh cached values
                var count = await ctx.InventoryItems.CountAsync(stoppingToken);
                Console.WriteLine($"[InventorySyncService] Synced {count} inventory items at {DateTime.Now}.");

                try
                {
                    // Wait for 10 minutes before the next synchronization
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Handle task cancellation
                    Console.WriteLine($"[InventorySyncService] Task was cancelled at {DateTime.Now}.");
                    break;

                }
            }
        }
    }
}