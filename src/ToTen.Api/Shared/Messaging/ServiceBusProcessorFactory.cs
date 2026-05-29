using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace ToTen.Api.Shared.Messaging;

/// <summary>
/// Fallback pattern for native SDK: Decoupled processor management
/// </summary>
public interface IIntegrationEventHandler<T> { Task HandleAsync(T message); }

public class ServiceBusProcessorFactory
{
    private readonly ServiceBusClient _client;
    private readonly IServiceProvider _serviceProvider;

    public ServiceBusProcessorFactory(ServiceBusClient client, IServiceProvider serviceProvider)
    {
        _client = client;
        _serviceProvider = serviceProvider;
    }

    public ServiceBusProcessor CreateProcessor(string topicName, string subscriptionName)
    {
        var processor = _client.CreateProcessor(topicName, subscriptionName);
        
        processor.ProcessMessageAsync += async args =>
        {
            using var scope = _serviceProvider.CreateScope();
            // Custom logic to resolve handler based on message type/subject
            // This allows scaling individual processors independently
            await Task.CompletedTask; 
        };

        return processor;
    }
}
