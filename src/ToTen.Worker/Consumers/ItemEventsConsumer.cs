using Rebus.Handlers;
using ToTen.Contracts;

namespace ToTen.Worker.Consumers;

public class ItemEventsHandler :
    IHandleMessages<ItemMovedEvent>,
    IHandleMessages<ItemListingEvent>,
    IHandleMessages<ItemTransferredEvent>
{
    private readonly ILogger<ItemEventsHandler> _logger;

    public ItemEventsHandler(ILogger<ItemEventsHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ItemMovedEvent message)
    {
        _logger.LogInformation("Processing ItemMovedEvent: Item {ItemId} moved to {ToLocationId}",
            message.ItemId, message.ToLocationId);
        return Task.CompletedTask;
    }

    public Task Handle(ItemListingEvent message)
    {
        _logger.LogInformation("Processing ItemListingEvent: Item {ItemId} listed for {Price}",
            message.ItemId, message.Price);
        return Task.CompletedTask;
    }

    public Task Handle(ItemTransferredEvent message)
    {
        _logger.LogInformation("Processing ItemTransferredEvent: Item {ItemId} transferred from {From} to {To}",
            message.ItemId, message.FromOwnerId, message.ToOwnerId);
        return Task.CompletedTask;
    }
}
