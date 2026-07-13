using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Authorization;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Storage.CreateBox;

public static class CreateBoxEndpoint
{
    public static void MapCreateBox(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/boxes", async (
            CreateBoxRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            IAuthorizationService authorizationService,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var location = await context.Locations.FindAsync(request.LocationId);
            if (location is null)
            {
                return Results.NotFound("Target location not found.");
            }

            var locationAuthResult = await authorizationService.AuthorizeAsync(principal, location, new ResourceOwnerRequirement());
            if (!locationAuthResult.Succeeded)
            {
                return Results.Forbid();
            }

            if (request.OrganizationId.HasValue)
            {
                var userIdString = user.Id.ToString();
                var isMember = await context.OrganizationMemberships
                    .AnyAsync(m => m.OrganizationId == request.OrganizationId && m.UserId == userIdString);
                if (!isMember && !user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
                {
                    return Results.Forbid();
                }
            }

            var box = new Box
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                LocationId = request.LocationId,
                OwnerId = user.Id.ToString(),
                OrganizationId = request.OrganizationId
            };

            context.Boxes.Add(box);
            await context.SaveChangesAsync();

            var response = new BoxResponse(box.Id, box.Name, box.LocationId, box.OrganizationId, box.ManifestId);

            return Results.Created($"/api/boxes/{box.Id}", response);
        })
        .WithName("CreateBox")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces<BoxResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
