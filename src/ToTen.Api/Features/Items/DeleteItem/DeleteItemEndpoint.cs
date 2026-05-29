using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ToTen.Api.Data;
using ToTen.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Shared.Messaging;

namespace ToTen.Api.Features.Items.DeleteItem;

public static class DeleteItemEndpoint
{
    public static void MapDeleteItem(this IEndpointRouteBuilder app)
    {
        // DELETE /items/122233-434d-43434....
        app.MapDelete("/{id}", async (
            Guid id,
            ToTenContext dbContext,
            IEventPublisher eventPublisher,
            ILogger<Program> logger,
            ClaimsPrincipal user) =>
        {
            var userEmail = user?.FindFirstValue(JwtRegisteredClaimNames.Email);

            if (string.IsNullOrEmpty(userEmail))
            {
                return Results.Unauthorized();
            }

            // Delete the item using the efficient ExecuteDeleteAsync
            await dbContext.InventoryItems
                     .Where(item => item.Id == id)
                     .ExecuteDeleteAsync();

            // Publish ItemDeleted event
            var itemDeletedEvent = new ItemDeletedEvent(
                ItemId: id,
                UserId: userEmail
            );

            try
            {
                await eventPublisher.PublishAsync(itemDeletedEvent);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the request
                logger.LogError(ex, "Failed to publish ItemDeleted event for item {ItemId}", id);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
