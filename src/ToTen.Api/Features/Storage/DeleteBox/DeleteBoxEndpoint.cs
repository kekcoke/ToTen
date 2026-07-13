using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Storage.DeleteBox;

public static class DeleteBoxEndpoint
{
    public static void MapDeleteBox(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/boxes/{id:guid}", async (
            Guid id,
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

            var hasItems = await context.InventoryItems.AnyAsync(i => i.BoxId == id);
            if (hasItems)
            {
                return Results.Conflict("Cannot delete a box that still contains items. Move them first.");
            }

            context.Boxes.Remove(box);
            await context.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("DeleteBox")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
