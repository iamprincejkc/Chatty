namespace Chatty.Api.Data.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string SessionId { get; set; } = default!;
    public string SenderRole { get; set; } = "customer";
    public string User { get; set; } = default!;
    public string Message { get; set; } = default!;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
