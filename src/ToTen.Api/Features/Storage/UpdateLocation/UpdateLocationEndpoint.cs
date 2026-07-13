using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ToTen.Api.Data;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Storage.UpdateLocation;

public static class UpdateLocationEndpoint
{
    public static void MapUpdateLocation(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/locations/{id:guid}", async (
            Guid id,
            UpdateLocationRequest request,
            ToTenContext context,
            IAuthorizationService authorizationService,
            ClaimsPrincipal principal) =>
        {
            var location = await context.Locations.FindAsync(id);
            if (location is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, location, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            if (location.OrganizationId.HasValue && !string.Equals(location.Name, request.Name, StringComparison.Ordinal))
            {
                // Postgres unique indexes treat distinct NULLs as non-equal, so this check
                // (mirroring the DB's (OrganizationId, Name) unique index) only applies once
                // a location is org-scoped — personal (org-less) locations may share names.
                var nameTaken = await context.Locations
                    .AnyAsync(l => l.Id != id && l.OrganizationId == location.OrganizationId && l.Name == request.Name);
                if (nameTaken)
                {
                    return Results.Conflict($"A location named '{request.Name}' already exists in this organization.");
                }
            }

            location.Name = request.Name;
            location.Coordinates = request.Latitude.HasValue && request.Longitude.HasValue
                ? new Point(request.Longitude.Value, request.Latitude.Value) { SRID = 4326 }
                : null;

            await context.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("UpdateLocation")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
