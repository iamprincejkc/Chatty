using FastEndpoints;
using Chatty.Api.Data;
using Chatty.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Chatty.Api.Contracts;

public class AssignAgentRequest
{
    public string SessionId { get; set; } = default!;
    public string AgentName { get; set; } = default!;
    public string AgentConnectionId { get; set; } = default!;
}

public class AssignAgentEndpoint : Endpoint<AssignAgentRequest, string>
{
    private readonly ChatDbContext _db;
    private readonly IAgentSessionTracker _agentTracker;

    public AssignAgentEndpoint(ChatDbContext db, IAgentSessionTracker agentTracker)
    {
        _db = db;
        _agentTracker = agentTracker;
    }

    public override void Configure()
    {
        Post("/api/assign-agent");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AssignAgentRequest req, CancellationToken ct)
    {
        var existing = await _db.AgentSessions
            .FirstOrDefaultAsync(x => x.SessionId == req.SessionId, ct);

        if (existing is not null)
        {
            // Check if that agent is still connected in memory
            var isGhost = !_agentTracker.AgentSessions.ContainsKey(existing.SessionId);

            if (!isGhost && existing.AgentName != req.AgentName)
            {
                await SendAsync($"Already handled by {existing.AgentName}");
                return;
            }

            // If ghost or same agent, allow reassignment by updating the record
            existing.AgentName = req.AgentName;
            existing.AgentConnectionId = req.AgentConnectionId;
        }
        else
        {
            _db.AgentSessions.Add(new AgentSession
            {
                SessionId = req.SessionId,
                AgentName = req.AgentName,
                AgentConnectionId = req.AgentConnectionId
            });
        }

        await _db.SaveChangesAsync(ct);
        await SendAsync("Assigned");
    }
}