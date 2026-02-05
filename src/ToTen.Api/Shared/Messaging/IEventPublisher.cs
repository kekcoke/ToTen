namespace ToTen.Api.Shared.Messaging;

/// <summary>
/// Interface for event publishing
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T eventMessage, CancellationToken cancellationToken = default);
}
