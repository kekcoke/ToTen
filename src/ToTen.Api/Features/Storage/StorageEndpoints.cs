using ToTen.Api.Features.Storage.CreateLocation;
using ToTen.Api.Features.Storage.MoveItem;

namespace ToTen.Api.Features.Storage;

public static class StorageEndpoints
{
    public static void MapStorageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateLocation();
        app.MapMoveItem();
    }
}
