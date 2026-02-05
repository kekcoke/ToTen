using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ToTen.Api.Data;
using ToTen.Contracts.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Shared.Messaging;

namespace ToTen.Api.Features.Items.UpdateItem;

public static class UpdateItemEndpoint
{
    public static void MapUpdateItem(this IEndpointRouteBuilder app)
    {
        // PUT /items/122233-434d-43434....
        app.MapPut("/{id}", async (
            Guid id,
            UpdateItemDto itemDto,
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

            var existingItem = await dbContext.Items.FindAsync(id);

            if (existingItem is null)
            {
                return Results.NotFound();
            }

            existingItem.Name = itemDto.Name;
            existingItem.CategoryId = itemDto.CategoryId;
            existingItem.Price = itemDto.Price;
            existingItem.ReleaseDate = itemDto.ReleaseDate;
            existingItem.Description = itemDto.Description;
            existingItem.LastUpdatedBy = userEmail;

            await dbContext.SaveChangesAsync();

            // Publish ItemUpdated event
            var itemUpdatedEvent = new ItemUpdatedEvent(
                ItemId: existingItem.Id,
                Name: existingItem.Name,
                CategoryId: existingItem.CategoryId,
                Price: existingItem.Price,
                UserId: userEmail
            );

            try
            {
                await eventPublisher.PublishAsync(itemUpdatedEvent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish ItemUpdated event for item {ItemId}", existingItem.Id);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
