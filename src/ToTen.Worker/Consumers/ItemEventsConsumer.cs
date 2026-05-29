using MassTransit;
using ToTen.Contracts;
using ToTen.Worker.Services;

namespace ToTen.Worker.Consumers;

public class ItemEventsConsumer : 
    IConsumer<ItemMovedEvent>,
    IConsumer<ItemListingEvent>,
    IConsumer<ItemTransferredEvent>
{
    private readonly ILogger<ItemEventsConsumer> _logger;

    public ItemEventsConsumer(ILogger<ItemEventsConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ItemMovedEvent> context)
    {
        _logger.LogInformation("Processing ItemMovedEvent: Item {ItemId} moved to {ToLocationId}", 
            context.Message.ItemId, context.Message.ToLocationId);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<ItemListingEvent> context)
    {
        _logger.LogInformation("Processing ItemListingEvent: Item {ItemId} listed for {Price}", 
            context.Message.ItemId, context.Message.Price);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<ItemTransferredEvent> context)
    {
        _logger.LogInformation("Processing ItemTransferredEvent: Item {ItemId} transferred from {From} to {To}", 
            context.Message.ItemId, context.Message.FromOwnerId, context.Message.ToOwnerId);
        return Task.CompletedTask;
    }
}
