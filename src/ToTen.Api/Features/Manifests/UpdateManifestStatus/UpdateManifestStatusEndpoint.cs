using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Manifests.CreateManifest;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Manifests.UpdateManifestStatus;

public static class UpdateManifestStatusEndpoint
{
    // Valid forward moves: Draft->Pending, Pending->InTransit, InTransit->Received.
    // Valid cancellations: Draft->Cancelled, Pending->Cancelled, InTransit->Cancelled.
    // Everything else (same-status, moves out of Received/Cancelled, backward moves, skipping
    // states) is rejected.
    private static readonly HashSet<(ManifestStatus From, ManifestStatus To)> ValidTransitions =
    [
        (ManifestStatus.Draft, ManifestStatus.Pending),
        (ManifestStatus.Pending, ManifestStatus.InTransit),
        (ManifestStatus.InTransit, ManifestStatus.Received),
        (ManifestStatus.Draft, ManifestStatus.Cancelled),
        (ManifestStatus.Pending, ManifestStatus.Cancelled),
        (ManifestStatus.InTransit, ManifestStatus.Cancelled),
    ];

    public static void MapUpdateManifestStatus(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/manifests/{id:guid}/status", async (
            Guid id,
            UpdateManifestStatusRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var manifest = await context.Manifests.FindAsync(id);
            if (manifest == null) return Results.NotFound();

            var userIdString = user.Id.ToString();
            var isOwner = await context.OrganizationMemberships.AnyAsync(m =>
                m.OrganizationId == manifest.OrganizationId && m.UserId == userIdString && m.Role == "Owner");

            if (!isOwner && !user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
            {
                return Results.Forbid();
            }

            if (!ValidTransitions.Contains((manifest.Status, request.Status)))
            {
                return Results.BadRequest($"Cannot transition manifest from {manifest.Status} to {request.Status}.");
            }

            manifest.Status = request.Status;
            await context.SaveChangesAsync();

            return Results.Ok(new ManifestResponse(
                manifest.Id,
                manifest.SourceLocationId,
                manifest.DestinationLocationId,
                manifest.Status,
                manifest.OrganizationId));
        })
        .WithName("UpdateManifestStatus")
        .WithTags("Manifests")
        .RequireAuthorization()
        .Produces<ManifestResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
