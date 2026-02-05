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
