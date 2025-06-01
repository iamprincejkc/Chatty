
using Chatty.Api.Contracts;
using Chatty.Api.Data;
using Chatty.Api.Hubs;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using static GetSessionsEndpoint;

public class GetSessionsEndpoint : EndpointWithoutRequest<List<SessionInfo>>
{
    private readonly ChatDbContext _db;
    private readonly IAgentSessionTracker _tracker;

    public GetSessionsEndpoint(ChatDbContext db, IAgentSessionTracker tracker)
    {
        _db = db;
        _tracker = tracker;
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
        var activeCustomerSessionIds = ChatHub.GetActiveCustomerSessions();

        var latestMessages = await _db.ChatMessages
            .Where(m => activeCustomerSessionIds.Contains(m.SessionId))
            .GroupBy(m => m.SessionId)
            .Select(g => g.OrderByDescending(m => m.SentAt).FirstOrDefault())
            .ToListAsync(ct);

        var assigned = await _db.AgentSessions
            .ToDictionaryAsync(x => x.SessionId, x => x.AgentName, ct);

        var result = latestMessages
            .OrderByDescending(m => m.SentAt)
            .Select((msg, index) => new SessionInfo
            {
                SessionId = msg!.SessionId,
                AssignedAgent = assigned.ContainsKey(msg.SessionId) ? assigned[msg.SessionId] : null,
                IpAddress = msg.IpAddress,
                Label = $"User {index + 1}"
            })
            .ToList();

        await SendAsync(result);
    }

}