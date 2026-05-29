namespace ToTen.Api.Features.Items.GetItem;

public record GetItemResponse(
    Guid Id,
    string Name,
    Guid CategoryId,
    string Description,
    string LastUpdatedBy);
