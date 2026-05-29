namespace ToTen.Api.Features.Items.UpdateItem;

public record UpdateItemRequest(
    string Name,
    string Description,
    Guid CategoryId);
