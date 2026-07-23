using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rebus.Bus;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Contracts.Events;

namespace ToTen.Api.Features.Marketplace.Shared;

public static class RefundProcessing
{
    /// <summary>
    /// Cumulative amount already refunded (Completed rows) against a transaction.
    /// </summary>
    public static async Task<decimal> RefundedToDateAsync(ToTenContext context, Guid transactionId)
    {
        return await context.Refunds
            .Where(r => r.TransactionId == transactionId && r.Status == RefundStatus.Completed)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;
    }

    /// <summary>
    /// Records a Completed refund against a transaction. When the cumulative refunded amount
    /// reaches the transaction total (a full refund), reverses the ownership transfer by writing
    /// an ItemLineage row moving the item back to the seller — mirroring OfferAcceptance's
    /// lineage-write. Partial refunds are financial-only (no ownership change). Publishes
    /// RefundIssuedEvent once the row is persisted. Callers must validate the amount against the
    /// remaining balance (see RefundedToDateAsync) before invoking.
    /// </summary>
    public static async Task<Refund> ProcessAsync(
        ToTenContext context,
        IBus bus,
        Transaction transaction,
        decimal amount,
        string reason,
        string initiatedBy)
    {
        var refundedToDate = await RefundedToDateAsync(context, transaction.Id);
        var isFullRefund = refundedToDate + amount >= transaction.Amount;

        var refund = new Refund
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Amount = amount,
            Reason = reason,
            InitiatedBy = initiatedBy,
            Status = RefundStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Refunds.Add(refund);

        if (isFullRefund)
        {
            var item = await context.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == transaction.InventoryItemId);
            if (item != null)
            {
                context.ItemLineages.Add(new ItemLineage
                {
                    Id = Guid.NewGuid(),
                    InventoryItemId = item.Id,
                    Action = "RefundReversal",
                    TransactionId = transaction.Id,
                    ChangedByUserId = initiatedBy,
                    Timestamp = DateTimeOffset.UtcNow,
                    StateSnapshot = JsonDocument.Parse(JsonSerializer.Serialize(item))
                });

                item.OwnerId = transaction.SellerId;
            }
        }

        await context.SaveChangesAsync();

        await bus.Publish(new RefundIssuedEvent(
            refund.Id,
            transaction.Id,
            transaction.InventoryItemId,
            refund.Amount,
            isFullRefund));

        return refund;
    }
}
