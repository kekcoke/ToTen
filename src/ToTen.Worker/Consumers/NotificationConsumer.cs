using Rebus.Handlers;
using ToTen.Contracts;
using ToTen.Worker.Services;

namespace ToTen.Worker.Consumers;

public class NotificationHandler : IHandleMessages<SendNotificationEvent>
{
    private readonly INotifier _notifier;
    private readonly ILogger<NotificationHandler> _logger;

    public NotificationHandler(INotifier notifier, ILogger<NotificationHandler> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(SendNotificationEvent message)
    {
        _logger.LogInformation("Sending notification to user {UserId} via {Channel}", message.UserId, message.Channel);

        if (message.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            await _notifier.SendEmailAsync("user@example.com", "Notification", message.Message);
        }
        else
        {
            await _notifier.SendSmsAsync("555-0199", message.Message);
        }
    }
}
