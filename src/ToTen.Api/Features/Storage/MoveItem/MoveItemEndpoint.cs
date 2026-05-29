using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Data;
using ToTen.Api.Shared.Identity;
using ToTen.Contracts;

namespace ToTen.Api.Features.Storage.MoveItem;

public static class MoveItemEndpoint
{
    public static void MapMoveItem(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/items/{itemId:guid}/move", async (
            Guid itemId,
            MoveItemRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            IPublishEndpoint publishEndpoint,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var item = await context.InventoryItems.FindAsync(itemId);
            if (item == null) return Results.NotFound();

            // Verify ownership
            if (item.OwnerId != user.Id.ToString())
            {
                return Results.Forbid();
            }

            // If moving to a new location, verify access to that location
            if (request.LocationId.HasValue)
            {
                var location = await context.Locations.FindAsync(request.LocationId.Value);
                if (location == null) return Results.BadRequest("Target location not found.");
                if (location.OwnerId != user.Id.ToString()) return Results.Forbid();
            }

            // If moving to a box, verify access to that box
            if (request.BoxId.HasValue)
            {
                var box = await context.Boxes.FindAsync(request.BoxId.Value);
                if (box == null) return Results.BadRequest("Target box not found.");
                if (box.OwnerId != user.Id.ToString()) return Results.Forbid();
            }

            var oldLocationId = item.LocationId ?? Guid.Empty;
            
            item.LocationId = request.LocationId;
            item.BoxId = request.BoxId;
            item.LastUpdatedBy = user.Email;

            await context.SaveChangesAsync();

            var movedAt = DateTime.UtcNow;

            // Publish event via MassTransit
            await publishEndpoint.Publish(new ItemMovedEvent(
                item.Id,
                oldLocationId,
                request.LocationId ?? Guid.Empty,
                movedAt));

            return Results.Ok(new MoveItemResponse(item.Id, item.LocationId, item.BoxId, movedAt));
        })
        .WithName("MoveItem")
        .WithTags("Storage")
        .RequireAuthorization();
    }
}
