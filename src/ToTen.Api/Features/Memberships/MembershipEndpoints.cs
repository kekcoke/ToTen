using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ToTen.Api.Features.Memberships;

public static class MembershipEndpoints
{
    public static void MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations/{orgId:guid}/members").WithTags("Memberships").RequireAuthorization();

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
        });

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
