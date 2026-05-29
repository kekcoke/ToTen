namespace ToTen.Api.Features.Marketplace.CreateListing;

public record CreateListingRequest(
    Guid InventoryItemId,
    decimal Price,
    DateOnly ReleaseDate);

public record ListingResponse(
    Guid Id,
    Guid InventoryItemId,
    decimal Price,
    DateOnly ReleaseDate,
    bool IsActive);
