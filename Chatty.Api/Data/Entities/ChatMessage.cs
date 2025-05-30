namespace Chatty.Api.Data.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string User { get; set; } = default!;
    public string Message { get; set; } = default!;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
