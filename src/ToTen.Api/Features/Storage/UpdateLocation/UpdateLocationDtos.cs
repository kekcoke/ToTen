using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Features.Storage.UpdateLocation;

public record UpdateLocationRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Range(-90, 90)] double? Latitude,
    [property: Range(-180, 180)] double? Longitude);
