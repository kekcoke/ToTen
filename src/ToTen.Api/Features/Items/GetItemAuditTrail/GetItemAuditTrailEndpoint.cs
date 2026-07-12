using System.Security.Claims;
using ToTen.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Shared.Authorization;

namespace ToTen.Api.Features.Items.GetItemAuditTrail;

public static class GetItemAuditTrailEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetItemAuditTrail(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id}/audit", async (
            Guid id,
            HttpContext httpContext,
            ToTenContext context,
            IAuthorizationService authorizationService,
            ClaimsPrincipal principal,
            int page = 1,
            int pageSize = 20) =>
        {
            var item = await context.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item is null)
            {
                return Results.NotFound();
            }

            var authResult = await authorizationService.AuthorizeAsync(principal, item, new ResourceOwnerRequirement());
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = context.AuditLogEntries
                .Where(e => e.ItemId == id)
                .OrderByDescending(e => e.OccurredAt);

            var totalCount = await query.CountAsync();

            // Materialize first, then project — Payload.RootElement (JsonDocument) can't be translated to SQL.
            var pageEntries = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var entries = pageEntries
                .Select(e => new AuditLogEntryResponse(
                    e.Id,
                    e.EventType,
                    e.ActorId,
                    e.OccurredAt,
                    e.RecordedAt,
                    e.Payload.RootElement))
                .ToList();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return Results.Ok(entries);
        })
        .WithName("GetItemAuditTrail")
        .WithTags("Items")
        .Produces<List<AuditLogEntryResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
