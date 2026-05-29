namespace ToTen.Api.Features.Marketplace.Search;

public record SearchListingsRequest(
    string? Text = null,
    Guid? CategoryId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    Guid? OrganizationId = null,
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusInKm = null,
    string? SortBy = null, // "Price", "Distance"
    int Page = 1,
    int PageSize = 20
);

public record SearchListingResponse(
    Guid Id,
    string ItemName,
    string CategoryName,
    decimal Price,
    double? DistanceInKm,
    Guid? OrganizationId
);

public record SearchListingsResponse(
    IEnumerable<SearchListingResponse> Listings,
    int TotalCount
);
