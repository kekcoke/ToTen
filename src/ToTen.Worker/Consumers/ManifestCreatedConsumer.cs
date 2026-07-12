using System.Text.Json;
using Rebus.Handlers;
using ToTen.Contracts.Events;
using ToTen.Worker.Data;

namespace ToTen.Worker.Consumers;

public class ManifestCreatedHandler : IHandleMessages<ManifestCreatedEvent>
{
    private readonly WorkerDbContext _db;
    private readonly ILogger<ManifestCreatedHandler> _logger;

    public ManifestCreatedHandler(WorkerDbContext db, ILogger<ManifestCreatedHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(ManifestCreatedEvent message)
    {
        _logger.LogInformation("Processing ManifestCreatedEvent: Manifest {ManifestId} for Org {OrgId}",
            message.ManifestId, message.OrganizationId);

        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            EventType = "ManifestCreated",
            ManifestId = message.ManifestId,
            ActorId = null,
            OccurredAt = DateTimeOffset.UtcNow,
            RecordedAt = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToDocument(message)
        });

        await _db.SaveChangesAsync();
    }
}
