namespace ToTen.Worker.Services;

public class NotificationOptions
{
    public const string SectionName = "Notifications";
    public string? SendGridApiKey { get; set; }
    public string? TwilioAccountSid { get; set; }
    public string? TwilioAuthToken { get; set; }
    public string? FromEmail { get; set; }
    public string? FromPhoneNumber { get; set; }
}
