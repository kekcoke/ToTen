using ToTen.Api.Data;
using ToTen.Api.Models;

namespace ToTen.Api.Features.Items.CreateItem;

public static class CreateItemEndpoint
{
    public static void MapCreateItem(this IEndpointRouteBuilder app)
    {
        app.MapPost("/items", async (CreateItemRequest request, ToTenContext context) =>
        {
            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                CategoryId = request.CategoryId,
                LastUpdatedBy = "System",
                OwnerId = "demo" // Hardcoded for initial phase
            };

            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();

            return Results.Created($"/items/{item.Id}", item);
        })
        .WithName("CreateItem")
        .WithTags("Items")
        .Produces<InventoryItem>(StatusCodes.Status201Created);
    }
}
