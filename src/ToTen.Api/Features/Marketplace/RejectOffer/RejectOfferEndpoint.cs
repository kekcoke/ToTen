using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.SubmitOffer;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.RateLimiting;
using System.Security.Claims;

namespace ToTen.Api.Features.Marketplace.RejectOffer;

public static class RejectOfferEndpoint
{
    public static void MapRejectOffer(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/offers/{offerId:guid}/reject", async (
            Guid offerId,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var offer = await context.Offers
                .Include(o => o.Listing)
                .ThenInclude(l => l!.InventoryItem)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null) return Results.NotFound("Offer not found.");
            var item = offer.Listing?.InventoryItem;
            if (item == null) return Results.InternalServerError("Linked item not found.");

            // Verify the current user is the owner of the item (seller)
            if (item.OwnerId != user.Id.ToString()) return Results.Forbid();
            if (offer.Status != OfferStatus.Pending) return Results.BadRequest("Only pending offers can be rejected.");

            offer.Status = OfferStatus.Rejected;
            await context.SaveChangesAsync();

            return Results.Ok(new OfferResponse(offer.Id, offer.ListingId, offer.Amount, offer.Status, offer.CounterAmount));
        })
        .WithName("RejectOffer")
        .WithTags("Marketplace")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);
    }
}
