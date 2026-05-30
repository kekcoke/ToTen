using ToTen.Api.Data;
using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ToTen.Api.Features.Items.UpdateItem;

public static class UpdateItemEndpoint
{
    public static void MapUpdateItem(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id}", async (Guid id, UpdateItemRequest request, ToTenContext context) =>
        {
            var item = await context.InventoryItems.FindAsync(id);

            if (item is null)
            {
                return Results.NotFound();
            }

            item.Name = request.Name;
            item.Description = request.Description;
            item.CategoryId = request.CategoryId;
            item.LastUpdatedBy = "System"; // Simplified for now

            await context.SaveChangesAsync();

            return Results.NoContent();
        })
        .WithName("UpdateItem")
        .WithTags("Items")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}
