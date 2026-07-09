using ToTen.Api.Models;
using ToTen.Api.Shared.Validation;

namespace ToTen.Api.Features.Manifests.CreateManifest;

public record CreateManifestRequest(
    [property: NotEmptyGuid] Guid SourceLocationId,
    [property: NotEmptyGuid] Guid DestinationLocationId,
    Guid? OrganizationId);

public record ManifestResponse(
    Guid Id,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    ManifestStatus Status,
    Guid? OrganizationId);
