using Rebus.Handlers;
using ToTen.Contracts;

namespace ToTen.Worker.Consumers;

public class ManifestCreatedHandler : IHandleMessages<ManifestCreatedEvent>
{
    private readonly ILogger<ManifestCreatedHandler> _logger;

    public ManifestCreatedHandler(ILogger<ManifestCreatedHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ManifestCreatedEvent message)
    {
        _logger.LogInformation("Processing ManifestCreatedEvent: Manifest {ManifestId} for Org {OrgId}",
            message.ManifestId, message.OrganizationId);
        return Task.CompletedTask;
    }
}
