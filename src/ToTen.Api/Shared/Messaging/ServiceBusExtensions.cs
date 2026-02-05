using Azure.Messaging.ServiceBus;

namespace ToTen.Api.Shared.Messaging;

public static class ServiceBusExtensions
{
    /// <summary>
    /// Adds Azure Service Bus client and EventPublisher to the service collection
    /// </summary>
    public static WebApplicationBuilder AddServiceBusMessaging(this WebApplicationBuilder builder, string connectionName)
    {
        // Add Azure Service Bus client
        builder.AddAzureServiceBusClient(connectionName);

        // Register event publishing service
        builder.Services.AddScoped<IEventPublisher, EventPublisher>(serviceProvider =>
        {
            var serviceBusClient = serviceProvider.GetRequiredService<ServiceBusClient>();
            var logger = serviceProvider.GetRequiredService<ILogger<EventPublisher>>();

            // Create sender for the items queue
            // In production, you might want to configure this via settings
            var sender = serviceBusClient.CreateSender("items-events");

            return new EventPublisher(sender, logger);
        });

        return builder;
    }
}
