using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using LogiTrack.Models;

public class Order
{
    /// <summary>
    /// Represents a customer order.
    /// </summary>
    [Key]
    public int OrderId { get; set; }

    /// <summary>
    /// Represents the name of the customer who placed the order.
    /// </summary>
    public string CustomerName { get; set; }
    
    /// <summary>
    /// Represents the date the order was placed.
    /// </summary>
    public DateTime DatePlaced { get; set; }

    /// <summary>
    /// Represents the items included in the order.
    /// </summary>

/// <remarks>Navigation property to the associated inventory items.</remarks>
    public List<OrderProducts> OrderProducts { get; set; } = new List<OrderProducts>();

    /// <summary>
    /// Constructor to initialize an order.
    /// </summary>
    public Order()
    {
        OrderProducts = new List<OrderProducts>();
        DatePlaced = DateTime.Now;
        CustomerName = "Unknown";
    }

    /// <summary>
    /// Constructor to initialize an order with a customer name.
    /// </summary>
    public Order(string customerName)
    {
        CustomerName = customerName;
        DatePlaced = DateTime.Now;
        OrderProducts = new List<OrderProducts>();
    }
    /// <summary>
    /// Adds an item to the order.
    /// </summary>
    /// <param name="item"></param>
    /// <returns>True if the item was added successfully, false otherwise.</returns>
    public bool AddItem(InventoryItem item)
    {
        OrderProducts.Add(new OrderProducts { InventoryItem = item });
        return true;
    }

    /// <summary>
    /// Removes an item from the order.
    /// </summary>
    /// <param name="item"></param>
    /// <returns>True if the item was removed successfully, false otherwise.</returns>
    public bool RemoveItem(InventoryItem item)
    {
        var orderProduct = OrderProducts.Find(op => op.InventoryItem.ItemId == item.ItemId);
        if (orderProduct != null)
        {
            OrderProducts.Remove(orderProduct);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Displays a summary of the order.
    /// </summary>
    public void GetOrderSummary()
    {
        Console.WriteLine($"Order #{OrderId} for {CustomerName} | Items: {OrderProducts.Count} | Placed: {DatePlaced.ToShortDateString()}");
    }
}