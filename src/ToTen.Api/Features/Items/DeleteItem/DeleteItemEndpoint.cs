using System.Security.Claims;
using Rebus.Bus;
using ToTen.Api.Data;
using ToTen.Contracts.Events;
using Microsoft.AspNetCore.Authorization;
using ToTen.Api.Shared.Authorization;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Items.DeleteItem;

public static class DeleteItemEndpoint
{
    public static void MapDeleteItem(this IEndpointRouteBuilder app)
    {
        // DELETE /items/122233-434d-43434....
        app.MapDelete("/{id}", async (
            Guid id,
            ToTenContext dbContext,
            IAuthorizationService authorizationService,
            IIdentityManager identityManager,
            IBus bus,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var item = await dbContext.InventoryItems.FindAsync(id);
            if (item is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, item, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            dbContext.InventoryItems.Remove(item);
            await dbContext.SaveChangesAsync();

            await bus.Publish(new ItemDeletedEvent(
                ItemId: id,
                UserId: user.Id.ToString()
            ));

            return Results.NoContent();
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
