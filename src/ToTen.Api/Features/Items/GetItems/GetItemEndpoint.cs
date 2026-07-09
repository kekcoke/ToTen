using System.Security.Claims;
using ToTen.Api.Data;
using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Items.GetItems;

public static class GetItemsEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetItems(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
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

            IQueryable<InventoryItem> query = context.InventoryItems;

            if (!user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
            {
                var userId = user.Id.ToString();
                var memberOrgIds = await context.OrganizationMemberships
                    .Where(m => m.UserId == userId)
                    .Select(m => m.OrganizationId)
                    .ToListAsync();

                query = query.Where(i =>
                    i.OwnerId == userId ||
                    (i.OrganizationId != null && memberOrgIds.Contains(i.OrganizationId.Value)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(item => item.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => new GetItemsResponse(
                    item.Id,
                    item.Name,
                    item.CategoryId,
                    item.Description,
                    item.LastUpdatedBy))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return Results.Ok(items);
        })
        .WithName("GetItems")
        .WithTags("Items")
        .Produces<List<GetItemsResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
