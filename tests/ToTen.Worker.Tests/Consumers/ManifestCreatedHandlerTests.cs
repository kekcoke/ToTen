using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToTen.Contracts.Events;
using ToTen.Worker.Consumers;
using ToTen.Worker.Tests.Helpers;

namespace ToTen.Worker.Tests.Consumers;

/// <summary>
/// Covers audit finding 2.6: ManifestCreatedHandler now writes an AuditLogEntry row per event.
/// </summary>
[Collection("WorkerDb")]
public class ManifestCreatedHandlerTests(WorkerDbFixture fixture)
{
    [Fact]
    public async Task Handle_ManifestCreatedEvent_WritesAuditLogEntry()
    {
        var manifestId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await using var db = fixture.CreateContext();
        var handler = new ManifestCreatedHandler(db, NullLogger<ManifestCreatedHandler>.Instance);

        await handler.Handle(new ManifestCreatedEvent(manifestId, orgId, "Warehouse A", "Warehouse B"));

        await using var verifyDb = fixture.CreateContext();
        var entry = await verifyDb.AuditLogEntries.SingleAsync(e => e.ManifestId == manifestId, TestContext.Current.CancellationToken);
        Assert.Equal("ManifestCreated", entry.EventType);
        Assert.Null(entry.ItemId);
        Assert.Null(entry.ActorId);
    }
}
