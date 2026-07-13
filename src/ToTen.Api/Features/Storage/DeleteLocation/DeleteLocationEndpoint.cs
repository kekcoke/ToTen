using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Storage.DeleteLocation;

public static class DeleteLocationEndpoint
{
    public static void MapDeleteLocation(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/locations/{id:guid}", async (
            Guid id,
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

            var hasBoxes = await context.Boxes.AnyAsync(b => b.LocationId == id);
            if (hasBoxes)
            {
                return Results.Conflict("Cannot delete a location that still contains boxes. Move or delete them first.");
            }

            var hasItems = await context.InventoryItems.AnyAsync(i => i.LocationId == id);
            if (hasItems)
            {
                return Results.Conflict("Cannot delete a location that still contains items. Move them first.");
            }

            var hasManifests = await context.Manifests
                .AnyAsync(m => m.SourceLocationId == id || m.DestinationLocationId == id);
            if (hasManifests)
            {
                return Results.Conflict("Cannot delete a location that is referenced by a manifest.");
            }

            context.Locations.Remove(location);
            await context.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("DeleteLocation")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
