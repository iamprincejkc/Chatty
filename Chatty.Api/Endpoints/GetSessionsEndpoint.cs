
using Chatty.Api.Data;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using static GetSessionsEndpoint;

public class GetSessionsEndpoint : EndpointWithoutRequest<List<SessionInfo>>
{
    private readonly ChatDbContext _db;

    public GetSessionsEndpoint(ChatDbContext db)
    {
        _db = db;
    }

    public class SessionInfo
    {
        public string SessionId { get; set; } = default!;
        public string? AssignedAgent { get; set; }
        public string? IpAddress { get; set; }
        public string Label { get; set; } = default!;
    }

    public override void Configure()
    {
        Get("/api/sessions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Get latest message per session, ordered by SentAt
        var latestMessages = await _db.ChatMessages
            .GroupBy(m => m.SessionId)
            .Select(g => g.OrderByDescending(m => m.SentAt).FirstOrDefault())
            .ToListAsync(ct);

        // Load assigned agents
        var assigned = await _db.AgentSessions
            .ToDictionaryAsync(x => x.SessionId, x => x.AgentName, ct);

        var result = latestMessages
            .OrderByDescending(m => m!.SentAt)
            .Select((msg, index) => new SessionInfo
            {
                SessionId = msg!.SessionId,
                AssignedAgent = assigned.ContainsKey(msg.SessionId) ? assigned[msg.SessionId] : null,
                IpAddress = msg.IpAddress,
                Label = msg.User
            })
            .ToList();

        await SendAsync(result);
    }
}