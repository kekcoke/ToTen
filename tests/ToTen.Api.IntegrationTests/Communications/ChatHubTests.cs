using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Data;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Models;

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

    [Fact]
    public async Task SendMessage_TooLong_ThrowsHubException()
    {
        var connection = BuildConnection(factory);
        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);

            var tooLong = new string('a', 4001);

            await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(
                "SendMessage",
                factory.DefaultTestUserId.ToString(),
                tooLong,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await connection.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task SendMessage_ExceedsRateLimit_ThrowsHubException()
    {
        var connection = BuildConnection(factory);
        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);

            // Limit is 20 messages / 10s window (self-messages, so the
            // organization-membership check is bypassed and doesn't interfere).
            for (var i = 0; i < 20; i++)
            {
                await connection.InvokeAsync(
                    "SendMessage",
                    factory.DefaultTestUserId.ToString(),
                    "hi",
                    TestContext.Current.CancellationToken);
            }

            await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(
                "SendMessage",
                factory.DefaultTestUserId.ToString(),
                "hi",
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await connection.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task SendMessage_ToUserInDifferentOrganization_ThrowsHubException()
    {
        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ToTenContext>();

        var senderOrg = new Organization { Id = Guid.NewGuid(), Name = "Sender Org", Type = "Household" };
        var receiverOrg = new Organization { Id = Guid.NewGuid(), Name = "Receiver Org", Type = "Household" };
        var receiverId = Guid.NewGuid();
        ctx.Organizations.AddRange(senderOrg, receiverOrg);
        ctx.OrganizationMemberships.AddRange(
            new OrganizationMembership { OrganizationId = senderOrg.Id, UserId = factory.DefaultTestUserId.ToString() },
            new OrganizationMembership { OrganizationId = receiverOrg.Id, UserId = receiverId.ToString() });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var connection = BuildConnection(factory);
        try
        {
            await connection.StartAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(
                "SendMessage",
                receiverId.ToString(),
                "hello",
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await connection.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
