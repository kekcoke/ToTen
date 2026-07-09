using System.ComponentModel.DataAnnotations;
using ToTen.Api.Shared.Validation;

namespace ToTen.Api.Features.Items.UpdateItem;

public record UpdateItemRequest(
    [property: Required, StringLength(200)] string Name,
    [property: StringLength(2000)] string Description,
    [property: NotEmptyGuid] Guid CategoryId);
