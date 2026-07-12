using ToTen.Api.Features.Manifests.AssociateBoxes;
using ToTen.Api.Features.Manifests.CreateManifest;
using ToTen.Api.Features.Manifests.GenerateQR;
using ToTen.Api.Features.Manifests.GetManifest;
using ToTen.Api.Features.Manifests.GetManifests;
using ToTen.Api.Features.Manifests.UpdateManifestStatus;

namespace ToTen.Api.Features.Manifests;

public static class ManifestEndpoints
{
    public static void MapManifestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateManifest();
        app.MapGenerateQR();
        app.MapAssociateBoxes();
        app.MapGetManifest();
        app.MapGetManifests();
        app.MapUpdateManifestStatus();
    }
}
