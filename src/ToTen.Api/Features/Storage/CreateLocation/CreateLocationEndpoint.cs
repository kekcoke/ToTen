using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Storage.CreateLocation;

public static class CreateLocationEndpoint
{
    public static void MapCreateLocation(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/locations", async (
            CreateLocationRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            Point? coordinates = null;
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                coordinates = new Point(request.Longitude.Value, request.Latitude.Value) { SRID = 4326 };
            }

            var location = new ToTen.Api.Models.Location
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Coordinates = coordinates,
                OwnerId = user.Id.ToString(),
                OrganizationId = request.OrganizationId
            };

            context.Locations.Add(location);
            await context.SaveChangesAsync();

            var response = new LocationResponse(
                location.Id,
                location.Name,
                location.Coordinates?.Y,
                location.Coordinates?.X,
                location.OrganizationId);

            return Results.Created($"/api/locations/{location.Id}", response);
        })
        .WithName("CreateLocation")
        .WithTags("Storage")
        .RequireAuthorization();
    }
}
