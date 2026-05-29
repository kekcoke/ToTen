using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Communications;

[Authorize]
public class ChatHub : Hub
{
    private readonly IIdentityManager _identityManager;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IIdentityManager identityManager, ILogger<ChatHub> logger)
    {
        _identityManager = identityManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var user = _identityManager.GetCurrentUser(Context.User);
        _logger.LogInformation("User {UserId} connected to ChatHub", user?.Id);
        await base.OnConnectedAsync();
    }

    public async Task SendMessage(string receiverId, string message)
    {
        var sender = _identityManager.GetCurrentUser(Context.User);
        // Logic for persisting message and sending to specific user
        await Clients.User(receiverId).SendAsync("ReceiveMessage", sender?.Id, message);
    }
}
