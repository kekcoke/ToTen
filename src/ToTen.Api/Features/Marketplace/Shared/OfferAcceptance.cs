using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rebus.Bus;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Contracts.Events;

namespace ToTen.Api.Features.Marketplace.Shared;

public static class OfferAcceptance
{
    public static async Task<Transaction> AcceptAsync(
        ToTenContext context,
        IBus bus,
        Offer offer,
        decimal finalAmount,
        string actorId,
        string actorEmail)
    {
        var item = offer.Listing!.InventoryItem!;

        offer.Status = OfferStatus.Accepted;
        offer.Amount = finalAmount;
        offer.Listing.IsActive = false;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            InventoryItemId = item.Id,
            SellerId = item.OwnerId,
            BuyerId = offer.BuyerId,
            Amount = finalAmount,
            Timestamp = DateTimeOffset.UtcNow
        };
        context.Transactions.Add(transaction);

        var lineage = new ItemLineage
        {
            Id = Guid.NewGuid(),
            InventoryItemId = item.Id,
            Action = "OwnershipTransfer",
            TransactionId = transaction.Id,
            ChangedByUserId = actorId,
            Timestamp = DateTimeOffset.UtcNow,
            StateSnapshot = JsonDocument.Parse(JsonSerializer.Serialize(item))
        };
        context.ItemLineages.Add(lineage);

        var oldOwnerId = item.OwnerId;
        item.OwnerId = offer.BuyerId;
        item.LastUpdatedBy = actorEmail;

        var siblingOffers = await context.Offers
            .Where(o => o.ListingId == offer.ListingId
                && o.Id != offer.Id
                && (o.Status == OfferStatus.Pending || o.Status == OfferStatus.Countered))
            .ToListAsync();
        foreach (var sibling in siblingOffers)
        {
            sibling.Status = OfferStatus.Rejected;
        }

        await context.SaveChangesAsync();

        await bus.Publish(new ItemTransferredEvent(
            item.Id,
            oldOwnerId,
            item.OwnerId,
            transaction.Amount,
            transaction.Timestamp));

        // Transaction/lineage read surface (phase 8a): user ids are GUID-valued
        // (IIdentityManager models them as Guid), so parsing back is safe here.
        await bus.Publish(new ItemTransactionEvent(
            item.Id,
            transaction.Id,
            Guid.Parse(transaction.SellerId),
            Guid.Parse(transaction.BuyerId),
            transaction.Amount));

        return transaction;
    }
}
