using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Storage.CreateLocation;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Storage.GetLocations;

public static class GetLocationsEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetLocations(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/locations", async (
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

            IQueryable<Location> query = context.Locations;

            if (!user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
            {
                var userId = user.Id.ToString();
                var memberOrgIds = await context.OrganizationMemberships
                    .Where(m => m.UserId == userId)
                    .Select(m => m.OrganizationId)
                    .ToListAsync();

                query = query.Where(l => l.OwnerId == userId || (l.OrganizationId != null && memberOrgIds.Contains(l.OrganizationId.Value)));
            }

            var totalCount = await query.CountAsync();

            var locations = await query
                .OrderBy(l => l.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new LocationResponse(
                    l.Id,
                    l.Name,
                    l.Coordinates != null ? l.Coordinates.Y : (double?)null,
                    l.Coordinates != null ? l.Coordinates.X : (double?)null,
                    l.OrganizationId))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return Results.Ok(locations);
        })
        .WithName("GetLocations")
        .WithTags("Storage")
        .RequireAuthorization()
        .Produces<List<LocationResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
