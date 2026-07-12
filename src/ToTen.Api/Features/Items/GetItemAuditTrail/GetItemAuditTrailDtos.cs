using System.Text.Json;

namespace ToTen.Api.Features.Items.GetItemAuditTrail;

public record AuditLogEntryResponse(
    Guid Id,
    string EventType,
    string? ActorId,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    JsonElement Payload);
