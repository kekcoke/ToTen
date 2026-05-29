namespace ToTen.Worker.Services;

public interface INotifier
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendSmsAsync(string phoneNumber, string message);
}
