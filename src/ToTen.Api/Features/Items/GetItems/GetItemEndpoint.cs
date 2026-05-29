using ToTen.Api.Data;
using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ToTen.Api.Features.Items.GetItems;

public static class GetItemsEndpoint
{
    public static void MapGetItems(this IEndpointRouteBuilder app)
    {
        app.MapGet("/items", async (ToTenContext context) =>
        {
            return await context.InventoryItems
                .Select(item => new GetItemsResponse(
                    item.Id,
                    item.Name,
                    item.CategoryId,
                    item.Description,
                    item.LastUpdatedBy))
                .ToListAsync();
        })
        .WithName("GetItems")
        .WithTags("Items")
        .Produces<List<GetItemsResponse>>(StatusCodes.Status200OK);
    }
}
