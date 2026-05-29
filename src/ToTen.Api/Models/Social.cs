namespace ToTen.Api.Models;

public class ChatThread
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    
    public ICollection<ChatMessage> Messages { get; set; } = [];
}

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public ChatThread? Thread { get; set; }
    
    public required string SenderId { get; set; }
    public required string Content { get; set; }
    public DateTimeOffset SentAt { get; set; }
}

public class Notification
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class NotificationPreference
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
}
