using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using MassTransit;
using ToTen.Contracts;

namespace ToTen.Api.Features.Manifests.CreateManifest;

public static class CreateManifestEndpoint
{
    public static void MapCreateManifest(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/manifests", async (
            CreateManifestRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            IPublishEndpoint publishEndpoint,
            System.Security.Claims.ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var manifest = new Manifest
            {
                Id = Guid.NewGuid(),
                SourceLocationId = request.SourceLocationId,
                DestinationLocationId = request.DestinationLocationId,
                Status = ManifestStatus.Draft,
                OrganizationId = request.OrganizationId
            };

            context.Manifests.Add(manifest);
            await context.SaveChangesAsync();

            // Publish event
            await publishEndpoint.Publish(new ManifestCreatedEvent(
                manifest.Id,
                manifest.OrganizationId ?? Guid.Empty,
                manifest.SourceLocationId.ToString(),
                manifest.DestinationLocationId.ToString()));

            var response = new ManifestResponse(
                manifest.Id,
                manifest.SourceLocationId,
                manifest.DestinationLocationId,
                manifest.Status,
                manifest.OrganizationId);

            return Results.Created($"/api/manifests/{manifest.Id}", response);
        })
        .WithName("CreateManifest")
        .WithTags("Manifests")
        .RequireAuthorization();
    }
}
