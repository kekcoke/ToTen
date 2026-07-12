using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToTen.Contracts.Events;
using ToTen.Worker.Consumers;
using ToTen.Worker.Tests.Helpers;

namespace ToTen.Worker.Tests.Consumers;

/// <summary>
/// Covers audit finding 2.6: ItemEventsHandler now writes an AuditLogEntry row per event
/// instead of only logging. Each test asserts the row that lands in a real (Testcontainers)
/// Postgres database, not just that Handle() completes without throwing.
/// </summary>
[Collection("WorkerDb")]
public class ItemEventsHandlerTests(WorkerDbFixture fixture)
{
    [Fact]
    public async Task Handle_ItemMovedEvent_WritesAuditLogEntry()
    {
        var itemId = Guid.NewGuid();
        var toLocationId = Guid.NewGuid();
        var movedAt = DateTime.UtcNow;

        await using var db = fixture.CreateContext();
        var handler = new ItemEventsHandler(db, NullLogger<ItemEventsHandler>.Instance);

        await handler.Handle(new ItemMovedEvent(itemId, Guid.NewGuid(), toLocationId, movedAt));

        await using var verifyDb = fixture.CreateContext();
        var entry = await verifyDb.AuditLogEntries.SingleAsync(e => e.ItemId == itemId, TestContext.Current.CancellationToken);
        Assert.Equal("ItemMoved", entry.EventType);
        Assert.Null(entry.ManifestId);
        Assert.Null(entry.ActorId);
        Assert.Equal(movedAt, entry.OccurredAt.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_ItemListingEvent_WritesAuditLogEntry()
    {
        var itemId = Guid.NewGuid();
        var listingId = Guid.NewGuid();

        await using var db = fixture.CreateContext();
        var handler = new ItemEventsHandler(db, NullLogger<ItemEventsHandler>.Instance);

        await handler.Handle(new ItemListingEvent(itemId, listingId, 42.50m));

        await using var verifyDb = fixture.CreateContext();
        var entry = await verifyDb.AuditLogEntries.SingleAsync(e => e.ItemId == itemId, TestContext.Current.CancellationToken);
        Assert.Equal("ItemListed", entry.EventType);
        Assert.Null(entry.ActorId);
    }

    [Fact]
    public async Task Handle_ItemTransferredEvent_WritesAuditLogEntry()
    {
        var itemId = Guid.NewGuid();

        await using var db = fixture.CreateContext();
        var handler = new ItemEventsHandler(db, NullLogger<ItemEventsHandler>.Instance);

        await handler.Handle(new ItemTransferredEvent(itemId, "owner-a", "owner-b", 10m, DateTimeOffset.UtcNow));

        await using var verifyDb = fixture.CreateContext();
        var entry = await verifyDb.AuditLogEntries.SingleAsync(e => e.ItemId == itemId, TestContext.Current.CancellationToken);
        Assert.Equal("ItemTransferred", entry.EventType);
        Assert.Equal("owner-b", entry.ActorId);
    }

    [Fact]
    public async Task Handle_ItemDeletedEvent_WritesAuditLogEntry()
    {
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();

        await using var db = fixture.CreateContext();
        var handler = new ItemEventsHandler(db, NullLogger<ItemEventsHandler>.Instance);

        await handler.Handle(new ItemDeletedEvent(itemId, userId));

        await using var verifyDb = fixture.CreateContext();
        var entry = await verifyDb.AuditLogEntries.SingleAsync(e => e.ItemId == itemId, TestContext.Current.CancellationToken);
        Assert.Equal("ItemDeleted", entry.EventType);
        Assert.Equal(userId, entry.ActorId);
    }
}
