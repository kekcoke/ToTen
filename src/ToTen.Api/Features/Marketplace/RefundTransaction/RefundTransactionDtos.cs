using ToTen.Api.Models;

namespace ToTen.Api.Features.Marketplace.RefundTransaction;

/// <summary>
/// Refund request. When <see cref="Full"/> is true, <see cref="Amount"/> is ignored and the
/// remaining balance is refunded; otherwise <see cref="Amount"/> is required and must not exceed
/// the remaining balance.
/// </summary>
public record RefundRequest(decimal? Amount, string Reason, bool Full = false);

public record RefundResponse(
    Guid Id,
    Guid TransactionId,
    decimal Amount,
    string Reason,
    string InitiatedBy,
    RefundStatus Status,
    DateTimeOffset CreatedAt);
