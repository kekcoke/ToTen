using Azure.Messaging.ServiceBus;
using System.Text.Json;
using ToTen.Contracts.Events;
using ToTen.Worker.Services;

namespace ToTen.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;
    private readonly ServiceBusClient serviceBusClient;
    private readonly ItemEventProcessor eventProcessor;
    private ServiceBusProcessor? processor;

    private readonly JsonSerializerOptions _jsonOptions;

    public Worker(
        ILogger<Worker> logger,
        ServiceBusClient serviceBusClient,
        ItemEventProcessor eventProcessor)
    {
        this.logger = logger;
        this.serviceBusClient = serviceBusClient;
        this.eventProcessor = eventProcessor;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create processor for the items-events queue/topic
        processor = serviceBusClient.CreateProcessor("items-events", new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 5,
            PrefetchCount = 10
        });

        // Add handler for processing messages
        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += ProcessErrorAsync;

        logger.LogInformation("Starting Service Bus message processor for items-events");

        // Start processing
        await processor.StartProcessingAsync(stoppingToken);

        logger.LogInformation("Service Bus message processor started. Waiting for messages...");

        // Wait until cancellation is requested
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var messageBody = args.Message.Body.ToString();
        var eventType = args.Message.Subject;

        logger.LogInformation("Received message of type {EventType} with correlation ID {CorrelationId}",
            eventType, args.Message.CorrelationId);

        try
        {
            // Process different event types
            switch (eventType)
            {
                case nameof(ItemCreatedEvent):
                    var itemCreatedEvent = JsonSerializer.Deserialize<ItemCreatedEvent>(messageBody, _jsonOptions);
                    if (itemCreatedEvent != null)
                    {
                        await eventProcessor.ProcessItemCreatedAsync(itemCreatedEvent, args.CancellationToken);
                    }
                    break;

                case nameof(ItemUpdatedEvent):
                    var itemUpdatedEvent = JsonSerializer.Deserialize<ItemUpdatedEvent>(messageBody, _jsonOptions);
                    if (itemUpdatedEvent != null)
                    {
                        await eventProcessor.ProcessItemUpdatedAsync(itemUpdatedEvent, args.CancellationToken);
                    }
                    break;

                case nameof(ItemDeletedEvent):
                    var itemDeletedEvent = JsonSerializer.Deserialize<ItemDeletedEvent>(messageBody, _jsonOptions);
                    if (itemDeletedEvent != null)
                    {
                        await eventProcessor.ProcessItemDeletedAsync(itemDeletedEvent, args.CancellationToken);
                    }
                    break;

                default:
                    logger.LogWarning("Unknown event type: {EventType}", eventType);
                    break;
            }

            // Complete the message
            await args.CompleteMessageAsync(args.Message);

            logger.LogInformation("Successfully processed message {MessageId} of type {EventType}",
                args.Message.MessageId, eventType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message {MessageId} of type {EventType}",
                args.Message.MessageId, eventType);

            // Abandon the message so it can be retried
            // Service Bus will automatically handle dead lettering after max retry attempts
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus processor error: {ErrorSource} - {FullyQualifiedNamespace} - {EntityPath}",
            args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Service Bus message processor");

        if (processor != null)
        {
            await processor.StopProcessingAsync(cancellationToken);
            await processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);

        logger.LogInformation("Service Bus message processor stopped");
    }

    public override void Dispose()
    {
        processor?.DisposeAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
