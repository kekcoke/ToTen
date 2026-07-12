using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.RateLimiting;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ToTen.Api.Features.Memberships;

public static class MembershipEndpoints
{
    private const int MaxPageSize = 100;

    public static void MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations/{orgId:guid}/members").WithTags("Memberships").RequireAuthorization();

        group.MapGet("/", async (
            Guid orgId,
            HttpContext httpContext,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal,
            int page = 1,
            int pageSize = 20) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var org = await context.Organizations.FindAsync(orgId);
            if (org == null || org.DateDeleted != null) return Results.NotFound();

            var userIdString = user.Id.ToString();
            var isMember = await context.OrganizationMemberships.AnyAsync(m =>
                m.OrganizationId == orgId && m.UserId == userIdString);

            if (!isMember && !user.Roles.Contains("admin") && !user.Roles.Contains("super_admin"))
                return Results.Forbid();

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = context.OrganizationMemberships
                .Where(m => m.OrganizationId == orgId)
                .OrderBy(m => m.UserId);

            var totalCount = await query.CountAsync();

            var members = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MemberResponse(m.UserId, m.Role))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return Results.Ok(members);
        });

        group.MapPatch("/{userId:guid}/role", async (
            Guid orgId,
            Guid userId,
            ChangeMemberRoleRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var currentUser = identityManager.GetCurrentUser(principal);
            if (currentUser == null) return Results.Unauthorized();

            var isOwner = await context.OrganizationMemberships.AnyAsync(m =>
                m.OrganizationId == orgId && m.UserId == currentUser.Id.ToString() && m.Role == "Owner");

            if (!isOwner && !currentUser.Roles.Contains("admin") && !currentUser.Roles.Contains("super_admin"))
                return Results.Forbid();

            var targetUserIdString = userId.ToString();
            var membership = await context.OrganizationMemberships.FindAsync(orgId, targetUserIdString);
            if (membership == null) return Results.NotFound();

            if (string.Equals(membership.Role, "Owner", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                var ownerCount = await context.OrganizationMemberships.CountAsync(m =>
                    m.OrganizationId == orgId && m.Role == "Owner");

                if (ownerCount == 1)
                {
                    return Results.Problem(
                        detail: "Cannot change the role of the organization's only Owner. Promote another member to Owner first.",
                        statusCode: StatusCodes.Status400BadRequest);
                }
            }

            membership.Role = request.Role;
            await context.SaveChangesAsync();

            return Results.Ok(new MembershipResponse(membership.OrganizationId, Guid.Parse(membership.UserId), membership.Role));
        });

        group.MapPost("/", async (Guid orgId, InviteMemberRequest request, ToTenContext context, IIdentityManager identityManager, ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var userIdString = user.Id.ToString();

            // Verify the current user is an owner/admin of the organization
            var isOwner = await context.OrganizationMemberships.AnyAsync(m => 
                m.OrganizationId == orgId && m.UserId == userIdString && m.Role == "Owner");
            
            if (!isOwner && !user.Roles.Contains("admin")) return Results.Forbid();

            var membership = new OrganizationMembership
            {
                OrganizationId = orgId,
                UserId = request.UserId.ToString(),
                Role = request.Role
            };

            context.OrganizationMemberships.Add(membership);
            await context.SaveChangesAsync();

            return Results.Ok(new MembershipResponse(membership.OrganizationId, Guid.Parse(membership.UserId), membership.Role));
        })
        .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);

        group.MapDelete("/{userId:guid}", async (Guid orgId, Guid userId, ToTenContext context, IIdentityManager identityManager, ClaimsPrincipal principal) =>
        {
            var currentUser = identityManager.GetCurrentUser(principal);
            if (currentUser == null) return Results.Unauthorized();

            var targetUserIdString = userId.ToString();
            var membership = await context.OrganizationMemberships.FindAsync(orgId, targetUserIdString);
            if (membership == null) return Results.NotFound();

            // Only owners or admins can remove members
            var isOwner = await context.OrganizationMemberships.AnyAsync(m => 
                m.OrganizationId == orgId && m.UserId == currentUser.Id.ToString() && m.Role == "Owner");
            
            if (!isOwner && !currentUser.Roles.Contains("admin") && currentUser.Id != userId) 
                return Results.Forbid();

            context.OrganizationMemberships.Remove(membership);
            await context.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
