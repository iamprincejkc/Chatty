namespace Chatty.Api.Data.Entities;

public class AgentSession
{
    public int Id { get; set; }
    public string SessionId { get; set; } = default!;
    public string AgentName { get; set; } = default!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
