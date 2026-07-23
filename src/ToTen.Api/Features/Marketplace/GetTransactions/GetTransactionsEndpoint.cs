using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Shared.Identity;
using System.Security.Claims;

namespace ToTen.Api.Features.Marketplace.GetTransactions;

public static class GetTransactionsEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetTransactions(this IEndpointRouteBuilder app)
    {
        // List the caller's transactions (as either buyer or seller), most recent first.
        app.MapGet("/api/transactions", async (
            HttpContext httpContext,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal,
            int page = 1,
            int pageSize = 20) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();
            var userId = user.Id.ToString();

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = context.Transactions
                .Where(t => t.SellerId == userId || t.BuyerId == userId)
                .OrderByDescending(t => t.Timestamp);

            var totalCount = await query.CountAsync();
            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionResponse(
                    t.Id, t.InventoryItemId, t.SellerId, t.BuyerId, t.Amount, t.Timestamp))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();
            return Results.Ok(transactions);
        })
        .WithName("GetTransactions")
        .WithTags("Marketplace")
        .RequireAuthorization();

        // Single transaction plus its item lineage (purchase/ownership history),
        // gated to the buyer or seller party on that transaction.
        app.MapGet("/api/transactions/{transactionId:guid}", async (
            Guid transactionId,
            ToTenContext context,
            IIdentityManager identityManager,
            ClaimsPrincipal principal) =>
        {
            var user = identityManager.GetCurrentUser(principal);
            if (user == null) return Results.Unauthorized();
            var userId = user.Id.ToString();

            var transaction = await context.Transactions
                .FirstOrDefaultAsync(t => t.Id == transactionId);
            if (transaction == null) return Results.NotFound("Transaction not found.");

            if (transaction.SellerId != userId && transaction.BuyerId != userId)
                return Results.Forbid();

            var lineage = await context.ItemLineages
                .Where(l => l.TransactionId == transactionId)
                .OrderBy(l => l.Timestamp)
                .Select(l => new LineageEntryResponse(
                    l.Id, l.Action, l.TransactionId, l.ChangedByUserId, l.Timestamp))
                .ToListAsync();

            return Results.Ok(new TransactionDetailResponse(
                transaction.Id, transaction.InventoryItemId, transaction.SellerId,
                transaction.BuyerId, transaction.Amount, transaction.Timestamp, lineage));
        })
        .WithName("GetTransactionById")
        .WithTags("Marketplace")
        .RequireAuthorization();
    }
}
