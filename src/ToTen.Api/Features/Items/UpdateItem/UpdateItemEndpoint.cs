using System.Security.Claims;
using ToTen.Api.Data;
using ToTen.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Shared.Authorization;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Items.UpdateItem;

public static class UpdateItemEndpoint
{
    public static void MapUpdateItem(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id}", async (
            Guid id,
            UpdateItemRequest request,
            ToTenContext context,
            IAuthorizationService authorizationService,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var item = await context.InventoryItems.FindAsync(id);

            if (item is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, item, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            var user = identityManager.GetCurrentUser(principal);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            item.Name = request.Name;
            item.Description = request.Description;
            item.CategoryId = request.CategoryId;
            item.LastUpdatedBy = user.Email;

            await context.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("UpdateItem")
        .WithTags("Items")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
