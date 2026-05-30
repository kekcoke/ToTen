using Microsoft.AspNetCore.SignalR.Client;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Communications;

public class ChatHubTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    private HubConnection BuildConnection(ToTenWebApplicationFactory f)
    {
        // Force the server to start so Server.CreateHandler() is available
        f.CreateClient();

        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/chat", opts =>
            {
                opts.HttpMessageHandlerFactory = _ => f.Server.CreateHandler();
            })
            .Build();
    }

    private HubConnection BuildUnauthConnection()
    {
        var unauthFactory = factory.WithAuth(succeeds: false);
        unauthFactory.CreateClient();

        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/chat", opts =>
            {
                opts.HttpMessageHandlerFactory = _ => unauthFactory.Server.CreateHandler();
            })
            .Build();
    }

    [Fact]
    public async Task ChatHub_AuthenticatedUser_CanConnect()
    {
        var connection = BuildConnection(factory);
        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);
            Assert.Equal(HubConnectionState.Connected, connection.State);
        }
        finally
        {
            await connection.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ChatHub_UnauthenticatedUser_ConnectionFails()
    {
        var connection = BuildUnauthConnection();

        await Assert.ThrowsAnyAsync<Exception>(
            () => connection.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChatHub_SendMessage_ReceiveMessageFires()
    {
        var connection = BuildConnection(factory);
        var tcs = new TaskCompletionSource<(object? senderId, string message)>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<object, string>("ReceiveMessage", (senderId, message) =>
        {
            tcs.TrySetResult((senderId, message));
        });

        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);

            // Send message to self (DefaultTestUserId) — simplest way to verify
            // the hub routes messages without requiring a second connected user
            await connection.InvokeAsync(
                "SendMessage",
                factory.DefaultTestUserId.ToString(),
                "hello",
                TestContext.Current.CancellationToken);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000, TestContext.Current.CancellationToken));
            Assert.Same(tcs.Task, completed);
            var (_, receivedMsg) = await tcs.Task;
            Assert.Equal("hello", receivedMsg);
        }
        finally
        {
            await connection.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
