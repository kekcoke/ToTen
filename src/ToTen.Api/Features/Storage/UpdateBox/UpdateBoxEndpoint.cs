using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ToTen.Api.Data;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Storage.UpdateBox;

public static class UpdateBoxEndpoint
{
    public static void MapUpdateBox(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/boxes/{id:guid}", async (
            Guid id,
            UpdateBoxRequest request,
            ToTenContext context,
            IAuthorizationService authorizationService,
            ClaimsPrincipal principal) =>
        {
            var box = await context.Boxes.FindAsync(id);
            if (box is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, box, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            if (request.LocationId != box.LocationId)
            {
                var newLocation = await context.Locations.FindAsync(request.LocationId);
                if (newLocation is null)
                {
                    return Results.NotFound("Target location not found.");
                }

                var locationAuthResult = await authorizationService.AuthorizeAsync(principal, newLocation, new ResourceOwnerRequirement());
                if (!locationAuthResult.Succeeded)
                {
                    return Results.Forbid();
                }
            }

            box.Name = request.Name;
            box.LocationId = request.LocationId;

            await context.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("UpdateBox")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
