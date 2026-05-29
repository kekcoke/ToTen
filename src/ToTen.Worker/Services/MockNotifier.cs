namespace ToTen.Worker.Services;

public class MockNotifier : INotifier
{
    private readonly ILogger<MockNotifier> _logger;

    public MockNotifier(ILogger<MockNotifier> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("[MOCK EMAIL] To: {To}, Subject: {Subject}", to, subject);
        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string phoneNumber, string message)
    {
        _logger.LogInformation("[MOCK SMS] To: {PhoneNumber}, Message: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
