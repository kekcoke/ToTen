using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Storage.CreateLocation;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Storage.GetLocation;

public static class GetLocationEndpoint
{
    public static void MapGetLocation(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/locations/{id:guid}", async (
            Guid id,
            ToTenContext context,
            IAuthorizationService authorizationService,
            ClaimsPrincipal principal) =>
        {
            var location = await context.Locations.FirstOrDefaultAsync(l => l.Id == id);

            if (location is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, location, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            return Results.Ok(new LocationResponse(
                location.Id,
                location.Name,
                location.Coordinates?.Y,
                location.Coordinates?.X,
                location.OrganizationId));
        })
        .WithName("GetLocation")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces<LocationResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
