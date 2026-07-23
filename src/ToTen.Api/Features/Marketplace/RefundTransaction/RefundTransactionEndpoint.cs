using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.Shared;
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.RateLimiting;
using Rebus.Bus;
using System.Security.Claims;

namespace ToTen.Api.Features.Marketplace.RefundTransaction;

public static class RefundTransactionEndpoint
{
    public static void MapRefundTransaction(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/transactions/{transactionId:guid}/refunds", async (
            Guid transactionId,
            RefundRequest request,
            ToTenContext context,
            IIdentityManager identityManager,
            IBus bus,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();

            var transaction = await context.Transactions
                .FirstOrDefaultAsync(t => t.Id == transactionId);
            if (transaction == null) return Results.NotFound("Transaction not found.");

            // Only the seller on the transaction may issue a refund.
            if (transaction.SellerId != user.Id.ToString()) return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Reason))
                return Results.BadRequest("Reason is required.");

            var refundedToDate = await RefundProcessing.RefundedToDateAsync(context, transaction.Id);
            var remaining = transaction.Amount - refundedToDate;
            if (remaining <= 0)
                return Results.BadRequest("Transaction is already fully refunded.");

            // full:true short-circuits the amount to the remaining balance.
            var amount = request.Full ? remaining : (request.Amount ?? 0m);
            if (amount <= 0)
                return Results.BadRequest("Refund amount must be greater than zero.");
            if (amount > remaining)
                return Results.BadRequest($"Refund amount {amount} exceeds the remaining balance {remaining}.");

            var refund = await RefundProcessing.ProcessAsync(
                context, bus, transaction, amount, request.Reason, user.Id.ToString());

            return Results.Created($"/api/transactions/{transactionId}/refunds/{refund.Id}",
                new RefundResponse(
                    refund.Id, refund.TransactionId, refund.Amount, refund.Reason,
                    refund.InitiatedBy, refund.Status, refund.CreatedAt));
        })
        .WithName("RefundTransaction")
        .WithTags("Marketplace")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingConfiguration.StrictPolicy);
    }
}
