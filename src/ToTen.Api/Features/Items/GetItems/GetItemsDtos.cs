namespace ToTen.Api.Features.Items.GetItems;

public record GetItemsResponse(
    Guid Id,
    string Name,
    Guid CategoryId,
    string Description,
    string LastUpdatedBy);
