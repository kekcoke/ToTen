using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Features.Communications;

[Authorize]
public class ChatHub : Hub
{
    private const int MaxMessageLength = 4000;
    private const int MaxMessagesPerWindow = 20;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(10);
    private const string RateLimitItemsKey = "ChatHub.RateLimit";

    private readonly IIdentityManager _identityManager;
    private readonly ToTenContext _context;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IIdentityManager identityManager, ToTenContext context, ILogger<ChatHub> logger)
    {
        _identityManager = identityManager;
        _context = context;
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
        if (sender is null)
        {
            throw new HubException("Not authenticated.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new HubException("Message cannot be empty.");
        }

        if (message.Length > MaxMessageLength)
        {
            throw new HubException($"Message exceeds the {MaxMessageLength}-character limit.");
        }

        if (!Guid.TryParse(receiverId, out _))
        {
            throw new HubException("Invalid receiver.");
        }

        EnforceRateLimit();

        var isSelfMessage = receiverId == sender.Id.ToString();
        if (!isSelfMessage && !sender.Roles.Contains("admin") && !sender.Roles.Contains("super_admin"))
        {
            var senderOrgIds = await _context.OrganizationMemberships
                .Where(m => m.UserId == sender.Id.ToString())
                .Select(m => m.OrganizationId)
                .ToListAsync();

            var sharesOrganization = senderOrgIds.Count > 0 && await _context.OrganizationMemberships
                .AnyAsync(m => m.UserId == receiverId && senderOrgIds.Contains(m.OrganizationId));

            if (!sharesOrganization)
            {
                throw new HubException("You can only message users who share an organization with you.");
            }
        }

        // Persistence to ChatMessage/ChatThread intentionally deferred — see
        // docs/section-2-flagged-issues.md (schema has no sender/receiver
        // participant model to persist a 1:1 direct message against yet).
        await Clients.User(receiverId).SendAsync("ReceiveMessage", sender.Id, message);
    }

    private void EnforceRateLimit()
    {
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset windowStart = now;
        var count = 0;

        if (Context.Items.TryGetValue(RateLimitItemsKey, out var raw) && raw is ValueTuple<DateTimeOffset, int> existing)
        {
            (windowStart, count) = existing;
        }

        if (now - windowStart > RateLimitWindow)
        {
            windowStart = now;
            count = 0;
        }

        count++;
        Context.Items[RateLimitItemsKey] = (windowStart, count);

        if (count > MaxMessagesPerWindow)
        {
            throw new HubException("Too many messages sent — slow down.");
        }
    }
}
