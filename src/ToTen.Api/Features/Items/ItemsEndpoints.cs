using ToTen.Api.Data;
using ToTen.Api.Features.Items.CreateItem;
using ToTen.Api.Features.Items.DeleteItem;
using ToTen.Api.Features.Items.GetItem;
using ToTen.Api.Features.Items.GetItems;
using ToTen.Api.Features.Items.UpdateItem;
using ToTen.Api.Models;

namespace ToTen.Api.Features.Items;

public static class ItemsEndpoints
{
    public static void MapItems(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/items");

        group.MapGetItems();
        group.MapGetItem();
        group.MapCreateItem();
        group.MapUpdateItem();
        group.MapDeleteItem();
    }
}
