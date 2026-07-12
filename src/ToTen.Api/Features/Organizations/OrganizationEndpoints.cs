using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.RateLimiting;
using System.Security.Claims;

namespace ToTen.Api.Features.Organizations;

public static class OrganizationEndpoints
{
    private const int MaxPageSize = 100;

    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations").WithTags("Organizations").RequireAuthorization();

        group.MapGet("/", async (
            HttpContext httpContext,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal,
            int page = 1,
            int pageSize = 20) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var userId = user.Id.ToString();
            var memberOrgIds = context.OrganizationMemberships
                .Where(m => m.UserId == userId)
                .Select(m => m.OrganizationId);

            var query = context.Organizations
                .Where(o => memberOrgIds.Contains(o.Id) && o.DateDeleted == null);

            var totalCount = await query.CountAsync();

            var orgs = await query
                .OrderBy(o => o.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrganizationResponse(o.Id, o.Name, o.Type))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return Results.Ok(orgs);
        });

        group.MapPost("/", async (CreateOrganizationRequest request, ToTenContext context, IIdentityManager identityManager, ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Type = request.Type
            };

            context.Organizations.Add(org);
            
            // Add creator as first member
            context.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = org.Id,
                UserId = user.Id.ToString(),
                Role = "Owner"
            });

            await context.SaveChangesAsync();

            return Results.Created($"/api/organizations/{org.Id}", new OrganizationResponse(org.Id, org.Name, org.Type));
        })
        .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);

        group.MapGet("/{id:guid}", async (Guid id, ToTenContext context, IIdentityManager identityManager, ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var org = await context.Organizations.FindAsync(id);
            if (org == null || org.DateDeleted != null) return Results.NotFound();

            var isMember = await context.OrganizationMemberships.AnyAsync(m =>
                m.OrganizationId == id && m.UserId == user.Id.ToString());

            if (!isMember && !user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
                return Results.Forbid();

            return Results.Ok(new OrganizationResponse(org.Id, org.Name, org.Type));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ToTenContext context, IIdentityManager identityManager, ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var org = await context.Organizations.FindAsync(id);
            if (org == null || org.DateDeleted != null) return Results.NotFound();

            var isOwner = await context.OrganizationMemberships.AnyAsync(m =>
                m.OrganizationId == id && m.UserId == user.Id.ToString() && m.Role == "Owner");

            if (!isOwner && !user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
                return Results.Forbid();

            org.DateDeleted = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapPatch("/{id:guid}", async (Guid id, RenameOrganizationRequest request, ToTenContext context, IIdentityManager identityManager, ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var org = await context.Organizations.FindAsync(id);
            if (org == null || org.DateDeleted != null) return Results.NotFound();

            var isOwner = await context.OrganizationMemberships.AnyAsync(m =>
                m.OrganizationId == id && m.UserId == user.Id.ToString() && m.Role == "Owner");

            if (!isOwner && !user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
                return Results.Forbid();

            org.Name = request.Name;
            await context.SaveChangesAsync();

            return Results.Ok(new OrganizationResponse(org.Id, org.Name, org.Type));
        });
    }
}
