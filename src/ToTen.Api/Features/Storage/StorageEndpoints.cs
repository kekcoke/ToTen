using ToTen.Api.Features.Storage.CreateBox;
using ToTen.Api.Features.Storage.CreateLocation;
using ToTen.Api.Features.Storage.DeleteBox;
using ToTen.Api.Features.Storage.DeleteLocation;
using ToTen.Api.Features.Storage.GetBox;
using ToTen.Api.Features.Storage.GetBoxes;
using ToTen.Api.Features.Storage.GetLocation;
using ToTen.Api.Features.Storage.GetLocations;
using ToTen.Api.Features.Storage.MoveItem;
using ToTen.Api.Features.Storage.UpdateBox;
using ToTen.Api.Features.Storage.UpdateLocation;

namespace ToTen.Api.Features.Storage;

public static class StorageEndpoints
{
    public static void MapStorageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateLocation();
        app.MapGetLocation();
        app.MapGetLocations();
        app.MapUpdateLocation();
        app.MapDeleteLocation();
        app.MapMoveItem();
        app.MapCreateBox();
        app.MapGetBox();
        app.MapGetBoxes();
        app.MapUpdateBox();
        app.MapDeleteBox();
    }
}
