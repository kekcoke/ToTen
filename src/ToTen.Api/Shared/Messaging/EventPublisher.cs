using Azure.Messaging.ServiceBus;
using System.Text.Json;

namespace ToTen.Api.Shared.Messaging;

/// <summary>
/// Service for publishing domain events to Azure Service Bus
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly ServiceBusSender sender;
    private readonly ILogger<EventPublisher> logger;
    private readonly JsonSerializerOptions jsonOptions;

    public EventPublisher(ServiceBusSender sender, ILogger<EventPublisher> logger)
    {
        this.sender = sender;
        this.logger = logger;
        jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var messageBody = JsonSerializer.Serialize(eventMessage, jsonOptions);
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                Subject = typeof(T).Name,
                MessageId = Guid.NewGuid().ToString(),
                ContentType = "application/json"
            };

            // Add custom properties for easier filtering/routing
            serviceBusMessage.ApplicationProperties["EventType"] = typeof(T).Name;

            // Try to get common properties using reflection (simple approach)
            var eventType = eventMessage?.GetType();
            var itemIdProp = eventType?.GetProperty("ItemId");
            var userIdProp = eventType?.GetProperty("UserId");
            var timestampProp = eventType?.GetProperty("Timestamp");

            if (itemIdProp?.GetValue(eventMessage) is Guid itemId)
            {
                serviceBusMessage.CorrelationId = itemId.ToString();
                serviceBusMessage.ApplicationProperties["ItemId"] = itemId.ToString();
            }

            if (userIdProp?.GetValue(eventMessage) is string userId)
            {
                serviceBusMessage.ApplicationProperties["UserId"] = userId;
            }

            if (timestampProp?.GetValue(eventMessage) is DateTime timestamp)
            {
                serviceBusMessage.ApplicationProperties["Timestamp"] = timestamp.ToString("O");
            }

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            logger.LogInformation("Published event {EventType}", typeof(T).Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish event {EventType}", typeof(T).Name);

            // In a production scenario, you might want to implement:
            // - Retry logic with exponential backoff
            // - Dead letter queue handling
            // - Circuit breaker pattern
            // - Store-and-forward pattern for guaranteed delivery
            throw;
        }
    }
}
