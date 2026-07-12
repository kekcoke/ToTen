using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.SubmitOffer;
using ToTen.Api.Shared.Identity;
using System.Security.Claims;

namespace ToTen.Api.Features.Marketplace.GetListingOffers;

public static class GetListingOffersEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetListingOffers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/listings/{listingId:guid}/offers", async (
            Guid listingId,
            HttpContext httpContext,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal,
            int page = 1,
            int pageSize = 20) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var listing = await context.Listings
                .Include(l => l.InventoryItem)
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null) return Results.NotFound("Listing not found.");
            var item = listing.InventoryItem;
            if (item == null) return Results.InternalServerError("Linked item not found.");

            // Only the seller (item owner) may review offers on their listing
            if (item.OwnerId != user.Id.ToString()) return Results.Forbid();

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = context.Offers
                .Where(o => o.ListingId == listingId)
                .OrderBy(o => o.Id);

            var totalCount = await query.CountAsync();
            var offers = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OfferResponse(o.Id, o.ListingId, o.Amount, o.Status, o.CounterAmount))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();

            return Results.Ok(offers);
        })
        .WithName("GetListingOffers")
        .WithTags("Marketplace")
        .RequireAuthorization();
    }
}
