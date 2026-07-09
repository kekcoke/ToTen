using Microsoft.Extensions.Logging.Abstractions;
using ToTen.Contracts.Events;
using ToTen.Worker.Consumers;

namespace ToTen.Worker.Tests.Consumers;

/// <summary>
/// First test coverage for the Worker's message consumers (audit findings 1.9/2.6 -
/// zero Worker tests existed before this). Each handler is currently log-only, so
/// these tests assert that every message type ItemEventsHandler is registered to
/// consume (including ItemDeletedEvent, added for audit finding 1.9) is actually
/// handled without throwing, rather than testing specific side effects that don't
/// exist yet.
/// </summary>
public class ItemEventsHandlerTests
{
    private static ItemEventsHandler CreateHandler() => new(NullLogger<ItemEventsHandler>.Instance);

    [Fact]
    public async Task Handle_ItemMovedEvent_CompletesWithoutThrowing()
    {
        var handler = CreateHandler();

        await handler.Handle(new ItemMovedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public async Task Handle_ItemListingEvent_CompletesWithoutThrowing()
    {
        var handler = CreateHandler();

        await handler.Handle(new ItemListingEvent(Guid.NewGuid(), Guid.NewGuid(), 42.50m));
    }

    [Fact]
    public async Task Handle_ItemTransferredEvent_CompletesWithoutThrowing()
    {
        var handler = CreateHandler();

        await handler.Handle(new ItemTransferredEvent(
            Guid.NewGuid(), "owner-a", "owner-b", 10m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Handle_ItemDeletedEvent_CompletesWithoutThrowing()
    {
        var handler = CreateHandler();

        await handler.Handle(new ItemDeletedEvent(Guid.NewGuid(), Guid.NewGuid().ToString()));
    }
}
