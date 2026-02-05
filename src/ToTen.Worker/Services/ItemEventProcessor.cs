using System.Text.Json;
using ToTen.Contracts.Events;

namespace ToTen.Worker.Services;

/// <summary>
/// Processes item-related events received from Service Bus
/// </summary>
public class ItemEventProcessor(ILogger<ItemEventProcessor> logger)
{
    public async Task ProcessItemCreatedAsync(ItemCreatedEvent eventData, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing ItemCreated event for item {ItemId}: {ItemName}",
            eventData.ItemId, eventData.Name);

        // Example business logic that could be implemented:
        // 1. Send notification email/push notification
        // 2. Update search index
        // 3. Update inventory cache
        // 4. Generate reports
        // 5. Trigger downstream processes

        try
        {
            // Simulate some processing work
            await Task.Delay(100, cancellationToken);

            // Example: Send notification (in real scenario, you'd inject an email service)
            logger.LogInformation(
                "Notification: New item '{ItemName}' created with price ${Price:F2}",
                eventData.Name, eventData.Price);

            logger.LogInformation("Successfully processed ItemCreated event for item {ItemId}", eventData.ItemId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing ItemCreated event for item {ItemId}", eventData.ItemId);
            throw; // Re-throw to trigger Service Bus retry/dead letter behavior
        }
    }

    public async Task ProcessItemUpdatedAsync(ItemUpdatedEvent eventData, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing ItemUpdated event for item {ItemId}: {ItemName}",
            eventData.ItemId, eventData.Name);

        try
        {
            await Task.Delay(50, cancellationToken);

            // Example business logic for item updates
            logger.LogInformation(
                "Notification: Item '{ItemName}' was updated with new price ${Price:F2}",
                eventData.Name, eventData.Price);

            logger.LogInformation("Successfully processed ItemUpdated event for item {ItemId}", eventData.ItemId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing ItemUpdated event for item {ItemId}", eventData.ItemId);
            throw;
        }
    }

    public async Task ProcessItemDeletedAsync(ItemDeletedEvent eventData, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing ItemDeleted event for item {ItemId}",
            eventData.ItemId);

        try
        {
            await Task.Delay(30, cancellationToken);

            // Example business logic for item deletion
            logger.LogInformation("Notification: Item '{ItemId}' has been deleted", eventData.ItemId);

            logger.LogInformation("Successfully processed ItemDeleted event for item {ItemId}", eventData.ItemId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing ItemDeleted event for item {ItemId}", eventData.ItemId);
            throw;
        }
    }
}
