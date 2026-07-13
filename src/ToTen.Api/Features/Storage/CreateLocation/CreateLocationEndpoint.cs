using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Storage.CreateLocation;

public static class CreateLocationEndpoint
{
    public static void MapCreateLocation(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/locations", async (
            CreateLocationRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

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

            if (request.OrganizationId.HasValue)
            {
                // Postgres unique indexes treat distinct NULLs as non-equal, so this check
                // (mirroring the DB's (OrganizationId, Name) unique index) only applies once
                // a location is org-scoped — personal (org-less) locations may share names.
                var nameTaken = await context.Locations
                    .AnyAsync(l => l.OrganizationId == request.OrganizationId && l.Name == request.Name);
                if (nameTaken)
                {
                    return Results.Conflict($"A location named '{request.Name}' already exists in this organization.");
                }
            }

            Point? coordinates = null;
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                coordinates = new Point(request.Longitude.Value, request.Latitude.Value) { SRID = 4326 };
            }

            var location = new ToTen.Api.Models.Location
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Coordinates = coordinates,
                OwnerId = user.Id.ToString(),
                OrganizationId = request.OrganizationId
            };

            context.Locations.Add(location);
            await context.SaveChangesAsync();

            var response = new LocationResponse(
                location.Id,
                location.Name,
                location.Coordinates?.Y,
                location.Coordinates?.X,
                location.OrganizationId);

            return Results.Created($"/api/locations/{location.Id}", response);
        })
        .WithName("CreateLocation")
        .WithTags("Storage")
        .RequireAuthorization();
    }
}
