using System.Text.Json;
using Rebus.Handlers;
using ToTen.Contracts.Events;
using ToTen.Worker.Data;

namespace ToTen.Worker.Consumers;

public class ItemEventsHandler :
    IHandleMessages<ItemMovedEvent>,
    IHandleMessages<ItemListingEvent>,
    IHandleMessages<ItemTransferredEvent>,
    IHandleMessages<ItemTransactionEvent>,
    IHandleMessages<ItemDeletedEvent>,
    IHandleMessages<RefundIssuedEvent>
{
    private readonly WorkerDbContext _db;
    private readonly ILogger<ItemEventsHandler> _logger;

    public ItemEventsHandler(WorkerDbContext db, ILogger<ItemEventsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(ItemMovedEvent message)
    {
        _logger.LogInformation("Processing ItemMovedEvent: Item {ItemId} moved to {ToLocationId}",
            message.ItemId, message.ToLocationId);

        await RecordAuditLogAsync("ItemMoved", itemId: message.ItemId, manifestId: null,
            actorId: null, occurredAt: message.MovedAt, message);
    }

    public async Task Handle(ItemListingEvent message)
    {
        _logger.LogInformation("Processing ItemListingEvent: Item {ItemId} listed for {Price}",
            message.ItemId, message.Price);

        await RecordAuditLogAsync("ItemListed", itemId: message.ItemId, manifestId: null,
            actorId: null, occurredAt: DateTimeOffset.UtcNow, message);
    }

    public async Task Handle(ItemTransferredEvent message)
    {
        _logger.LogInformation("Processing ItemTransferredEvent: Item {ItemId} transferred from {From} to {To}",
            message.ItemId, message.FromOwnerId, message.ToOwnerId);

        await RecordAuditLogAsync("ItemTransferred", itemId: message.ItemId, manifestId: null,
            actorId: message.ToOwnerId, occurredAt: message.Timestamp, message);
    }

    public async Task Handle(ItemTransactionEvent message)
    {
        _logger.LogInformation("Processing ItemTransactionEvent: Item {ItemId} sold (transaction {TransactionId}) for {Price}",
            message.ItemId, message.TransactionId, message.Price);

        await RecordAuditLogAsync("ItemTransaction", itemId: message.ItemId, manifestId: null,
            actorId: message.SellerId.ToString(), occurredAt: DateTimeOffset.UtcNow, message);
    }

    public async Task Handle(ItemDeletedEvent message)
    {
        _logger.LogInformation("Processing ItemDeletedEvent: Item {ItemId} deleted by {UserId}",
            message.ItemId, message.UserId);

        await RecordAuditLogAsync("ItemDeleted", itemId: message.ItemId, manifestId: null,
            actorId: message.UserId, occurredAt: message.Timestamp, message);
    }

    public async Task Handle(RefundIssuedEvent message)
    {
        _logger.LogInformation("Processing RefundIssuedEvent: Refund {RefundId} on transaction {TransactionId} for {Amount} (full: {IsFullRefund})",
            message.RefundId, message.TransactionId, message.Amount, message.IsFullRefund);

        await RecordAuditLogAsync("RefundIssued", itemId: message.InventoryItemId, manifestId: null,
            actorId: null, occurredAt: DateTimeOffset.UtcNow, message);
    }

    private async Task RecordAuditLogAsync(
        string eventType, Guid? itemId, Guid? manifestId, string? actorId, DateTimeOffset occurredAt, object payload)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            EventType = eventType,
            ItemId = itemId,
            ManifestId = manifestId,
            ActorId = actorId,
            OccurredAt = occurredAt,
            RecordedAt = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToDocument(payload)
        });

        await _db.SaveChangesAsync();
    }
}
