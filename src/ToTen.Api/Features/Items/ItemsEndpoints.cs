using ToTen.Api.Data;
using ToTen.Api.Features.Items.CreateItem;
using ToTen.Api.Features.Items.DeleteItem;
using ToTen.Api.Features.Items.GetItem;
using ToTen.Api.Features.Items.GetItemAuditTrail;
using ToTen.Api.Features.Items.GetItems;
using ToTen.Api.Features.Items.UpdateItem;
using ToTen.Api.Models;

namespace ToTen.Api.Features.Items;

public static class ItemsEndpoints
{
    public static void MapInventoryItems(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/items");

        group.WithTags("items")
            .RequireAuthorization();    
        group.MapGetItems();
        group.MapGetItem();
        group.MapGetItemAuditTrail();
        group.MapCreateItem();
        group.MapUpdateItem();
        group.MapDeleteItem();
    }
}
