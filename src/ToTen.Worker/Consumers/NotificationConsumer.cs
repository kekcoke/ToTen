using MassTransit;
using ToTen.Contracts;
using ToTen.Worker.Services;

namespace ToTen.Worker.Consumers;

public class NotificationConsumer : IConsumer<SendNotificationEvent>
{
    private readonly INotifier _notifier;
    private readonly ILogger<NotificationConsumer> _logger;

    public NotificationConsumer(INotifier notifier, ILogger<NotificationConsumer> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SendNotificationEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Sending notification to user {UserId} via {Channel}", msg.UserId, msg.Channel);

        if (msg.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            await _notifier.SendEmailAsync("user@example.com", "Notification", msg.Message);
        }
        else
        {
            await _notifier.SendSmsAsync("555-0199", msg.Message);
        }
    }
}
