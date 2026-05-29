namespace ToTen.Api.Features.Storage.CreateLocation;

public record CreateLocationRequest(
    string Name,
    double? Latitude,
    double? Longitude,
    Guid? OrganizationId);

public record LocationResponse(
    Guid Id,
    string Name,
    double? Latitude,
    double? Longitude,
    Guid? OrganizationId);
