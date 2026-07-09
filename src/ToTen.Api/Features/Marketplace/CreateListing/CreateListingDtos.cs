using System.ComponentModel.DataAnnotations;
using ToTen.Api.Shared.Validation;

namespace ToTen.Api.Features.Marketplace.CreateListing;

public record CreateListingRequest(
    [property: NotEmptyGuid] Guid InventoryItemId,
    [property: Range(0.01, double.MaxValue)] decimal Price,
    DateOnly ReleaseDate);

public record ListingResponse(
    Guid Id,
    Guid InventoryItemId,
    decimal Price,
    DateOnly ReleaseDate,
    bool IsActive);
