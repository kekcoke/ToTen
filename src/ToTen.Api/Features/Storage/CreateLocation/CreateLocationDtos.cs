using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Features.Storage.CreateLocation;

public record CreateLocationRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Range(-90, 90)] double? Latitude,
    [property: Range(-180, 180)] double? Longitude,
    Guid? OrganizationId);

public record LocationResponse(
    Guid Id,
    string Name,
    double? Latitude,
    double? Longitude,
    Guid? OrganizationId);
