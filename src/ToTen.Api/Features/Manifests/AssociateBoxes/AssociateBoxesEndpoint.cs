using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Manifests.AssociateBoxes;

public static class AssociateBoxesEndpoint
{
    public static void MapAssociateBoxes(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/manifests/{id:guid}/boxes", async (
            Guid id,
            AssociateBoxesRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            System.Security.Claims.ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var manifest = await context.Manifests.FindAsync(id);
            if (manifest == null) return Results.NotFound();

            var boxes = await context.Boxes
                .Where(b => request.BoxIds.Contains(b.Id))
                .ToListAsync();

            foreach (var box in boxes)
            {
                // Verify ownership of the box
                if (box.OwnerId == user.Id.ToString())
                {
                    box.ManifestId = id;
                }
            }

            await context.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("AssociateBoxesToManifest")
        .WithTags("Manifests")
        .RequireAuthorization();
    }
}
