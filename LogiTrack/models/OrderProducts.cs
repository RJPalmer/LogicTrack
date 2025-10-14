using System;
using LogiTrack.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Models;

[PrimaryKey(nameof(OrderId), nameof(ItemId))]
public class OrderProducts
{
    /// <summary>
    /// The ID of the order.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Navigation property to the associated order.
    /// </summary>
    public Order Order { get; set; }

    /// <summary>
    /// The ID of the inventory item.
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Navigation property to the associated inventory item.
    /// </summary>
    public InventoryItem InventoryItem { get; set; }

}