namespace ToTen.Api.Features.Items.CreateItem;

public record CreateItemRequest(
    string Name,
    string Description,
    Guid CategoryId);
