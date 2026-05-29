using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Shared.Authorization;
using ToTen.Api.Shared.Identity;
using System.Security.Claims;

namespace ToTen.Api.Features.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(AuthorizationConfiguration.AdminPolicy);

        // In a real implementation, these would interact with Keycloak Admin API
        // For Phase 2, we provide the slice structure and mock the response
        group.MapGet("/", () =>
        {
            var mockUsers = new[]
            {
                new UserResponse(Guid.NewGuid(), "admin@toten.com", new[] { "admin", "user" }),
                new UserResponse(Guid.NewGuid(), "user@toten.com", new[] { "user" })
            };
            return Results.Ok(mockUsers);
        });

        group.MapPut("/{id:guid}/roles", (Guid id, UpdateUserRolesRequest request) =>
        {
            // Logic to update roles in Keycloak/IAM
            return Results.NoContent();
        });
    }
}
