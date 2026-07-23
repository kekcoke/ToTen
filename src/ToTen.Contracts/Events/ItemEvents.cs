namespace ToTen.Contracts.Events;

/// <summary>
/// Published when a new item is created
/// </summary>
public record ItemCreatedEvent(
    Guid ItemId,
    string Name,
    Guid CategoryId,
    decimal Price,
    string UserId
)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when an item is updated
/// </summary>
public record ItemUpdatedEvent(
    Guid ItemId,
    string Name,
    Guid CategoryId,
    decimal Price,
    string UserId
)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published when an item is deleted
/// </summary>
public record ItemDeletedEvent(
    Guid ItemId,
    string UserId
)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record ItemMovedEvent(Guid ItemId, Guid FromLocationId, Guid ToLocationId, DateTime MovedAt);
public record ItemTransactionEvent(Guid ItemId, Guid TransactionId, Guid SellerId, Guid BuyerId, decimal Price);
public record ItemListingEvent(Guid ItemId, Guid ListingId, decimal Price);
public record ItemTransferredEvent(Guid ItemId, string FromOwnerId, string ToOwnerId, decimal Price, DateTimeOffset Timestamp);
public record ManifestCreatedEvent(Guid ManifestId, Guid OrganizationId, string Source, string Destination);
public record SendNotificationEvent(Guid UserId, string Message, string Channel);
public record RefundIssuedEvent(Guid RefundId, Guid TransactionId, Guid InventoryItemId, decimal Amount, bool IsFullRefund);
