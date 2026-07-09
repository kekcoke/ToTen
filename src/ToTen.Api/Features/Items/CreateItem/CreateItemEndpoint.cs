using System.Security.Claims;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.RateLimiting;

namespace ToTen.Api.Features.Items.CreateItem;

public static class CreateItemEndpoint
{
    public static void MapCreateItem(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (
            CreateItemRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                CategoryId = request.CategoryId,
                LastUpdatedBy = user.Email,
                OwnerId = user.Id.ToString(),
                OrganizationId = user.OrganizationId
            };

            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();

            return Results.Created($"/items/{item.Id}", item);
        })
        .WithName("CreateItem")
        .WithTags("Items")
        .Produces<InventoryItem>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);
    }
}
