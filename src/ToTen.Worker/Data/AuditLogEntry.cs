using System.Text.Json;

namespace ToTen.Worker.Data;

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public required string EventType { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ManifestId { get; set; }
    public string? ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public required JsonDocument Payload { get; set; }
}
