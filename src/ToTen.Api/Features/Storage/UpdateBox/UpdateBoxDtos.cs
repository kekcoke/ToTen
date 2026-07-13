using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Features.Storage.UpdateBox;

public record UpdateBoxRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Required] Guid LocationId);
