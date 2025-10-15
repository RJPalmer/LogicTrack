using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data;

/// <summary>
/// Database context for LogiTrack application.
/// </summary>
public class LogiTrackContext : DbContext
{
    /// <summary>
    /// Inventory items in the database.
    /// </summary>
    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;

    /// <summary>
    /// Orders in the database.
    /// </summary>
    public DbSet<Order> Orders { get; set; } = null!;

    public LogiTrackContext()
    {
    }

    public LogiTrackContext(DbContextOptions<LogiTrackContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Configures the database connection when options were not provided externally.
    /// </summary>
    /// <param name="options"></param>
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite("Data Source=logitrack.db");
        }
    }

    /// <summary>
    /// Configures the model.
    /// </summary>
    /// <param name="modelBuilder"></param> 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>()
        .ToTable("InventoryItems")
        .HasKey(i => i.ItemId);

        // Ensure EF treats ItemId as database-generated (autoincrement)
        modelBuilder.Entity<InventoryItem>()
            .Property(i => i.ItemId)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<InventoryItem>()
        .HasMany(i => i.OrderPr)
        .WithOne(op => op.InventoryItem)
        .HasForeignKey(op => op.ItemId);

        modelBuilder.Entity<Order>().ToTable("Orders")
        .HasMany(o => o.OrderProducts)
        .WithOne(op => op.Order)
        .HasForeignKey(op => op.OrderId);

        modelBuilder.Entity<OrderProducts>().ToTable("OrderProducts")
        .HasKey(op => new { op.OrderId, op.ItemId });
        modelBuilder.Entity<OrderProducts>()
        .HasOne(op => op.InventoryItem)
        .WithMany(i => i.OrderPr)
        .HasForeignKey(op => op.ItemId)
        .OnDelete(DeleteBehavior.Cascade)
        .IsRequired();
    }
}