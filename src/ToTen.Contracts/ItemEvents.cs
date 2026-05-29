namespace ToTen.Contracts;

public record ItemMovedEvent(Guid ItemId, Guid FromLocationId, Guid ToLocationId, DateTime MovedAt);
public record ItemTransactionEvent(Guid ItemId, Guid TransactionId, Guid SellerId, Guid BuyerId, decimal Price);
public record ItemListingEvent(Guid ItemId, Guid ListingId, decimal Price);
public record ItemTransferredEvent(Guid ItemId, string FromOwnerId, string ToOwnerId, decimal Price, DateTimeOffset Timestamp);
public record ManifestCreatedEvent(Guid ManifestId, Guid OrganizationId, string Source, string Destination);
public record SendNotificationEvent(Guid UserId, string Message, string Channel);
