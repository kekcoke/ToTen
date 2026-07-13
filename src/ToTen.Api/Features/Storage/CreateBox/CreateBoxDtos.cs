using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Features.Storage.CreateBox;

public record CreateBoxRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Required] Guid LocationId,
    Guid? OrganizationId);

public record BoxResponse(
    Guid Id,
    string Name,
    Guid LocationId,
    Guid? OrganizationId,
    Guid? ManifestId);
