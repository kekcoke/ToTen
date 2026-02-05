using Microsoft.Extensions.Logging;
using ToTen.Api.Shared.Messaging;

namespace ToTen.Api.IntegrationTests.Helpers;

/// <summary>
/// Mock EventPublisher for integration tests that doesn't actually publish events
/// </summary>
public class MockEventPublisher : IEventPublisher
{
    private readonly ILogger<MockEventPublisher> logger;

    public MockEventPublisher(ILogger<MockEventPublisher> logger)
    {
        this.logger = logger;
    }

    public async Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default)
    {
        // Log that the event would have been published but don't actually send it
        logger.LogInformation("Mock: Would publish event {EventType} with message {Message}",
            typeof(T).Name,
            System.Text.Json.JsonSerializer.Serialize(eventMessage));

        // Simulate async operation
        await Task.CompletedTask;
    }
}
