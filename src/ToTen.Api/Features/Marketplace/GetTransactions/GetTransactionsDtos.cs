namespace ToTen.Api.Features.Marketplace.GetTransactions;

public record TransactionResponse(
    Guid Id,
    Guid InventoryItemId,
    string SellerId,
    string BuyerId,
    decimal Amount,
    DateTimeOffset Timestamp);

public record LineageEntryResponse(
    Guid Id,
    string Action,
    Guid? TransactionId,
    string ChangedByUserId,
    DateTimeOffset Timestamp);

public record TransactionDetailResponse(
    Guid Id,
    Guid InventoryItemId,
    string SellerId,
    string BuyerId,
    decimal Amount,
    DateTimeOffset Timestamp,
    IReadOnlyList<LineageEntryResponse> Lineage);
