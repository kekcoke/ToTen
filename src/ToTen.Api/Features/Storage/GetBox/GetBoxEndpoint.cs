using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Storage.CreateBox;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Storage.GetBox;

public static class GetBoxEndpoint
{
    public static void MapGetBox(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/boxes/{id:guid}", async (
            Guid id,
            ToTenContext context,
            IAuthorizationService authorizationService,
            ClaimsPrincipal principal) =>
        {
            var box = await context.Boxes.FirstOrDefaultAsync(b => b.Id == id);

            if (box is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, box, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            return Results.Ok(new BoxResponse(box.Id, box.Name, box.LocationId, box.OrganizationId, box.ManifestId));
        })
        .WithName("GetBox")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces<BoxResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
