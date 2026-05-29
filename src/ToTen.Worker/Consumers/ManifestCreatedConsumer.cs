using MassTransit;
using ToTen.Contracts;

namespace ToTen.Worker.Consumers;

public class ManifestCreatedConsumer : IConsumer<ManifestCreatedEvent>
{
    private readonly ILogger<ManifestCreatedConsumer> _logger;

    public ManifestCreatedConsumer(ILogger<ManifestCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ManifestCreatedEvent> context)
    {
        _logger.LogInformation("Processing ManifestCreatedEvent: Manifest {ManifestId} for Org {OrgId}", 
            context.Message.ManifestId, context.Message.OrganizationId);
        return Task.CompletedTask;
    }
}
