using Chatty.Api.Data;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using static GetSessionsEndpoint;

public class GetSessionByIdEndpoint : Endpoint<GetSessionByIdEndpoint.Request, SessionInfo>
{
    private readonly ChatDbContext _db;

    public GetSessionByIdEndpoint(ChatDbContext db)
    {
        _db = db;
    }

    public class Request
    {
        public string SessionId { get; set; } = default!;
    }

    public override void Configure()
    {
        Get("/api/sessions/{SessionId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var lastMsg = await _db.ChatMessages
        .Where(m => m.SessionId == req.SessionId)
        .OrderByDescending(m => m.SentAt)
        .FirstOrDefaultAsync(ct);

        var assignedAgent = await _db.AgentSessions
            .Where(a => a.SessionId == req.SessionId)
            .Select(a => a.AgentName)
            .FirstOrDefaultAsync(ct);

        var sessionInfo = new SessionInfo
        {
            SessionId = req.SessionId,
            AssignedAgent = assignedAgent,
            IpAddress = lastMsg?.IpAddress ?? "unknown",
            Label = lastMsg?.User ?? "New User"
        };

        await SendAsync(sessionInfo);
    }
}
