using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Features.Marketplace.RefundTransaction;
using ToTen.Api.Shared.Identity;
using System.Security.Claims;

namespace ToTen.Api.Features.Marketplace.GetTransactionRefunds;

public static class GetTransactionRefundsEndpoint
{
    private const int MaxPageSize = 100;

    public static void MapGetTransactionRefunds(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/transactions/{transactionId:guid}/refunds", async (
            Guid transactionId,
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

            var transaction = await context.Transactions
                .FirstOrDefaultAsync(t => t.Id == transactionId);
            if (transaction == null) return Results.NotFound("Transaction not found.");

            // Either party to the transaction (buyer or seller) may view its refunds.
            if (transaction.SellerId != userId && transaction.BuyerId != userId)
                return Results.Forbid();

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = context.Refunds
                .Where(r => r.TransactionId == transactionId)
                .OrderBy(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var refunds = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RefundResponse(
                    r.Id, r.TransactionId, r.Amount, r.Reason, r.InitiatedBy, r.Status, r.CreatedAt))
                .ToListAsync();

            httpContext.Response.Headers["X-Total-Count"] = totalCount.ToString();
            return Results.Ok(refunds);
        })
        .WithName("GetTransactionRefunds")
        .WithTags("Marketplace")
        .RequireAuthorization();
    }
}
