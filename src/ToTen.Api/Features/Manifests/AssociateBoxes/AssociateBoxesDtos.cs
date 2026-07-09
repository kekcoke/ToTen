using System.ComponentModel.DataAnnotations;

namespace ToTen.Api.Features.Manifests.AssociateBoxes;

public record AssociateBoxesRequest([property: MinLength(1)] Guid[] BoxIds);
