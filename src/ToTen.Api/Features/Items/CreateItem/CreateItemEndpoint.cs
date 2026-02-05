using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ToTen.Api.Data;
using ToTen.Api.Features.Items.Constants;
using ToTen.Api.Models;
using ToTen.Contracts.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Shared.Messaging;

namespace ToTen.Api.Features.Items.CreateItem;

public static class CreateItemEndpoint
{
    public static void MapCreateItem(this IEndpointRouteBuilder app)
    {
        // POST /items
        app.MapPost("/", async (
            CreateItemDto itemDto,
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

            var item = new Item
            {
                Name = itemDto.Name,
                CategoryId = itemDto.CategoryId,
                Price = itemDto.Price,
                ReleaseDate = itemDto.ReleaseDate,
                Description = itemDto.Description,
                LastUpdatedBy = userEmail
            };

            dbContext.Items.Add(item);

            await dbContext.SaveChangesAsync();

            logger.LogInformation(
                "Created item {ItemName} with price {ItemPrice}",
                item.Name,
                item.Price);

            // Publish ItemCreated event
            var itemCreatedEvent = new ItemCreatedEvent(
                ItemId: item.Id,
                Name: item.Name,
                CategoryId: item.CategoryId,
                Price: item.Price,
                UserId: userEmail
            );

            try
            {
                await eventPublisher.PublishAsync(itemCreatedEvent);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the request
                // You might want to use the Outbox pattern here
                logger.LogError(ex, "Failed to publish ItemCreated event for item {ItemId}", item.Id);
            }

            return Results.CreatedAtRoute(
                EndpointNames.GetItem,
                new { id = item.Id },
                new ItemDetailsDto(
                    item.Id,
                    item.Name,
                    item.CategoryId,
                    item.Price,
                    item.ReleaseDate,
                    item.Description,
                    item.LastUpdatedBy
                ));
        })
        .RequireAuthorization()
        .Produces<ItemDetailsDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
