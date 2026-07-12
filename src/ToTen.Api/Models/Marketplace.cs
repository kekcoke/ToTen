namespace ToTen.Api.Models;

public class Listing
{
    public Guid Id { get; set; }
    
    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public decimal Price { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public bool IsActive { get; set; }
}

public class Offer
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    
    public required string BuyerId { get; set; }
    public decimal Amount { get; set; }
    public OfferStatus Status { get; set; }
    public decimal? CounterAmount { get; set; }
}

public enum OfferStatus
{
    Pending,
    Accepted,
    Rejected,
    Countered
}

public class Transaction
{
    public Guid Id { get; set; }
    public required string BuyerId { get; set; }
    public required string SellerId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    
    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
}
