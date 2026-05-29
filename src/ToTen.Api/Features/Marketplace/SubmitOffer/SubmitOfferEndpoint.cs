using Microsoft.AspNetCore.Mvc;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using System.Security.Claims;

namespace ToTen.Api.Features.Marketplace.SubmitOffer;

public static class SubmitOfferEndpoint
{
    public static void MapSubmitOffer(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/listings/{listingId:guid}/offers", async (
            Guid listingId,
            SubmitOfferRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var listing = await context.Listings.FindAsync(listingId);
            if (listing == null) return Results.NotFound("Listing not found.");
            if (!listing.IsActive) return Results.BadRequest("Listing is no longer active.");

            var offer = new Offer
            {
                Id = Guid.NewGuid(),
                ListingId = listingId,
                BuyerId = user.Id.ToString(),
                Amount = request.Amount,
                Status = OfferStatus.Pending
            };

            context.Offers.Add(offer);
            await context.SaveChangesAsync();

            var response = new OfferResponse(
                offer.Id,
                offer.ListingId,
                offer.Amount,
                offer.Status);

            return Results.Created($"/api/offers/{offer.Id}", response);
        })
        .WithName("SubmitOffer")
        .WithTags("Marketplace")
        .RequireAuthorization();
    }
}
