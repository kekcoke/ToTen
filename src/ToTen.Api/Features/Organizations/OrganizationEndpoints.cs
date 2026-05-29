using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using System.Security.Claims;

namespace ToTen.Api.Features.Organizations;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations").WithTags("Organizations").RequireAuthorization();

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
        });

        group.MapGet("/{id:guid}", async (Guid id, ToTenContext context) =>
        {
            var org = await context.Organizations.FindAsync(id);
            return org != null ? Results.Ok(new OrganizationResponse(org.Id, org.Name, org.Type)) : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ToTenContext context) =>
        {
            var org = await context.Organizations.FindAsync(id);
            if (org == null) return Results.NotFound();
            
            context.Organizations.Remove(org);
            await context.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
