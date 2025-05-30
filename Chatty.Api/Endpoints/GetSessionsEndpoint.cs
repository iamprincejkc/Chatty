
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
    }

    public override void Configure()
    {
        Get("/api/sessions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessions = await _db.ChatMessages
            .OrderByDescending(m => m.SentAt)
            .Select(m => m.SessionId)
            .Distinct()
            .ToListAsync(ct);

        var assigned = await _db.AgentSessions.ToDictionaryAsync(x => x.SessionId, x => x.AgentName, ct);

        var result = sessions.Select(sid => new SessionInfo
        {
            SessionId = sid,
            AssignedAgent = assigned.ContainsKey(sid) ? assigned[sid] : null
        }).ToList();

        await SendAsync(result);
    }
}