using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Manifests.CreateManifest;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Manifests.GetManifest;

public static class GetManifestEndpoint
{
    public static void MapGetManifest(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/manifests/{id:guid}", async (
            Guid id,
            ToTenContext context,
            IAuthorizationService authorizationService,
            ClaimsPrincipal principal) =>
        {
            var manifest = await context.Manifests
                .FirstOrDefaultAsync(m => m.Id == id);

            if (manifest is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, manifest, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            return Results.Ok(new ManifestResponse(
                manifest.Id,
                manifest.SourceLocationId,
                manifest.DestinationLocationId,
                manifest.Status,
                manifest.OrganizationId));
        })
        .WithName("GetManifest")
        .WithTags("Manifests")
        .RequireAuthorization()
        .Produces<ManifestResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
