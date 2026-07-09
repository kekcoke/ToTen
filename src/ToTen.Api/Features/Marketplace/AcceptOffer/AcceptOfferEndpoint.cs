using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.RateLimiting;
using Rebus.Bus;
using ToTen.Contracts;
using System.Security.Claims;
using System.Text.Json;

namespace ToTen.Api.Features.Marketplace.AcceptOffer;

public static class AcceptOfferEndpoint
{
    public static void MapAcceptOffer(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/offers/{offerId:guid}/accept", async (
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
            if (offer.Listing == null || !offer.Listing.IsActive) return Results.BadRequest("Listing is no longer active.");
            
            var item = offer.Listing.InventoryItem;
            if (item == null) return Results.InternalServerError("Linked item not found.");

            // Verify the current user is the owner of the item (seller)
            if (item.OwnerId != user.Id.ToString()) return Results.Forbid();

            // 1. Update Offer Status
            offer.Status = OfferStatus.Accepted;

            // 2. Close Listing
            offer.Listing.IsActive = false;

            // 3. Create Transaction Record
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                InventoryItemId = item.Id,
                SellerId = item.OwnerId,
                BuyerId = offer.BuyerId,
                Amount = offer.Amount,
                Timestamp = DateTimeOffset.UtcNow
            };
            context.Transactions.Add(transaction);

            // 4. Create Immutable Lineage Record
            var lineage = new ItemLineage
            {
                Id = Guid.NewGuid(),
                InventoryItemId = item.Id,
                Action = "OwnershipTransfer",
                TransactionId = transaction.Id,
                ChangedByUserId = user.Id.ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                StateSnapshot = JsonDocument.Parse(JsonSerializer.Serialize(item))
            };
            context.ItemLineages.Add(lineage);

            // 5. Transfer Ownership
            var oldOwnerId = item.OwnerId;
            item.OwnerId = offer.BuyerId;
            item.LastUpdatedBy = user.Email;

            await context.SaveChangesAsync();

            // 6. Publish Event
            await bus.Publish(new ItemTransferredEvent(
                item.Id,
                oldOwnerId,
                item.OwnerId,
                transaction.Amount,
                transaction.Timestamp));

            return Results.Ok(new { TransactionId = transaction.Id, NewOwnerId = item.OwnerId });
        })
        .WithName("AcceptOffer")
        .WithTags("Marketplace")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);
    }
}
