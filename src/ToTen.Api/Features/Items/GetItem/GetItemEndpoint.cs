using ToTen.Api.Data;
using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ToTen.Api.Features.Items.GetItem;

public static class GetItemEndpoint
{
    public static void MapGetItem(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id}", async (Guid id, ToTenContext context) =>
        {
            var item = await context.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id);

            return item is not null
                ? Results.Ok(new GetItemResponse(
                    item.Id,
                    item.Name,
                    item.CategoryId,
                    item.Description,
                    item.LastUpdatedBy))
                : Results.NotFound();
        })
        .WithName("GetItem")
        .WithTags("Items")
        .Produces<GetItemResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
