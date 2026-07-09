using System.Security.Claims;
using ToTen.Api.Data;
using ToTen.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Items.GetItem;

public static class GetItemEndpoint
{
    public static void MapGetItem(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id}", async (
            Guid id,
            ToTenContext context,
            IAuthorizationService authorizationService,
            ClaimsPrincipal principal) =>
        {
            var item = await context.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, item, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            return Results.Ok(new GetItemResponse(
                item.Id,
                item.Name,
                item.CategoryId,
                item.Description,
                item.LastUpdatedBy));
        })
        .WithName("GetItem")
        .WithTags("Items")
        .Produces<GetItemResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
