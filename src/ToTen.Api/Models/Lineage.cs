using System.Text.Json;

namespace ToTen.Api.Models;

public class ItemLineage
{
    public Guid Id { get; set; }
    
    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public required string Action { get; set; } // "OwnershipTransfer", "LocationChange", "PropertyUpdate"
    
    public Guid? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public required string ChangedByUserId { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    // JSONB Snapshot of the item state
    public JsonDocument? StateSnapshot { get; set; }
}
