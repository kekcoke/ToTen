using ToTen.Api.Models;

namespace ToTen.Api.Features.Manifests.CreateManifest;

public record CreateManifestRequest(
    Guid SourceLocationId,
    Guid DestinationLocationId,
    Guid? OrganizationId);

public record ManifestResponse(
    Guid Id,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    ManifestStatus Status,
    Guid? OrganizationId);
