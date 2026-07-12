using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.Shared;
using ToTen.Api.Features.Marketplace.SubmitOffer;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.RateLimiting;
using Rebus.Bus;
using System.Security.Claims;

namespace ToTen.Api.Features.Marketplace.RespondToCounterOffer;

public static class RespondToCounterOfferEndpoint
{
    public static void MapRespondToCounterOffer(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/offers/{offerId:guid}/counter/accept", async (
            Guid offerId,
            ToTenContext context,
            IIdentityManager identityManager,
            IBus bus,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var offer = await context.Offers
                .Include(o => o.Listing)
                .ThenInclude(l => l!.InventoryItem)
                .FirstOrDefaultAsync(o => o.Id == offerId);

            if (offer == null) return Results.NotFound("Offer not found.");

            // Verify the current user is the buyer who submitted the original offer
            if (offer.BuyerId != user.Id.ToString()) return Results.Forbid();
            if (offer.Status != OfferStatus.Countered || offer.CounterAmount is null)
                return Results.BadRequest("Offer has no pending counter to accept.");
            if (offer.Listing == null || !offer.Listing.IsActive) return Results.BadRequest("Listing is no longer active.");
            if (offer.Listing.InventoryItem == null) return Results.InternalServerError("Linked item not found.");

            var transaction = await OfferAcceptance.AcceptAsync(
                context, bus, offer, offer.CounterAmount.Value, user.Id.ToString(), user.Email);

            return Results.Ok(new { TransactionId = transaction.Id, NewOwnerId = offer.Listing.InventoryItem.OwnerId });
        })
        .WithName("AcceptCounterOffer")
        .WithTags("Marketplace")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);

        app.MapPost("/api/offers/{offerId:guid}/counter/reject", async (
            Guid offerId,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var offer = await context.Offers.FirstOrDefaultAsync(o => o.Id == offerId);
            if (offer == null) return Results.NotFound("Offer not found.");

            // Verify the current user is the buyer who submitted the original offer
            if (offer.BuyerId != user.Id.ToString()) return Results.Forbid();
            if (offer.Status != OfferStatus.Countered)
                return Results.BadRequest("Offer has no pending counter to reject.");

            offer.Status = OfferStatus.Rejected;
            await context.SaveChangesAsync();

            return Results.Ok(new OfferResponse(offer.Id, offer.ListingId, offer.Amount, offer.Status, offer.CounterAmount));
        })
        .WithName("RejectCounterOffer")
        .WithTags("Marketplace")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);
    }
}
