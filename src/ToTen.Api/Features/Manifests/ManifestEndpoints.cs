using ToTen.Api.Features.Manifests.CreateManifest;
using ToTen.Api.Features.Manifests.GenerateQR;

namespace ToTen.Api.Features.Manifests;

public static class ManifestEndpoints
{
    public static void MapManifestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateManifest();
        app.MapGenerateQR();
    }
}
