namespace ToTen.Api.Features.Storage.MoveItem;

public record MoveItemRequest(
    Guid? LocationId,
    Guid? BoxId);

public record MoveItemResponse(
    Guid ItemId,
    Guid? NewLocationId,
    Guid? NewBoxId,
    DateTime MovedAt);
