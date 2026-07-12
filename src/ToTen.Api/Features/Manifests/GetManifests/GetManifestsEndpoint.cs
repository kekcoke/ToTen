using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Manifests.CreateManifest;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Manifests.GetManifests;

public static class GetManifestsEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetManifests(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/manifests", async (
            HttpContext httpContext,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal,
            int page = 1,
            int pageSize = 20) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            IQueryable<Manifest> query = context.Manifests;

            if (!user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
            {
                var userId = user.Id.ToString();
                var memberOrgIds = await context.OrganizationMemberships
                    .Where(m => m.UserId == userId)
                    .Select(m => m.OrganizationId)
                    .ToListAsync();

                query = query.Where(m => memberOrgIds.Contains(m.OrganizationId));
            }

            var totalCount = await query.CountAsync();

            var manifests = await query
                .OrderBy(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new ManifestResponse(
                    m.Id,
                    m.SourceLocationId,
                    m.DestinationLocationId,
                    m.Status,
                    m.OrganizationId))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return Results.Ok(manifests);
        })
        .WithName("GetManifests")
        .WithTags("Manifests")
        .RequireAuthorization()
        .Produces<List<ManifestResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
