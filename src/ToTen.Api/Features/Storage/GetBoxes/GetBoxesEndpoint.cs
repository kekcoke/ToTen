using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Storage.CreateBox;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Storage.GetBoxes;

public static class GetBoxesEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetBoxes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/boxes", async (
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

            IQueryable<Box> query = context.Boxes;

            if (!user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
            {
                var userId = user.Id.ToString();
                var memberOrgIds = await context.OrganizationMemberships
                    .Where(m => m.UserId == userId)
                    .Select(m => m.OrganizationId)
                    .ToListAsync();

                query = query.Where(b => b.OwnerId == userId || (b.OrganizationId != null && memberOrgIds.Contains(b.OrganizationId.Value)));
            }

            var totalCount = await query.CountAsync();

            var boxes = await query
                .OrderBy(b => b.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BoxResponse(b.Id, b.Name, b.LocationId, b.OrganizationId, b.ManifestId))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return Results.Ok(boxes);
        })
        .WithName("GetBoxes")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces<List<BoxResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
