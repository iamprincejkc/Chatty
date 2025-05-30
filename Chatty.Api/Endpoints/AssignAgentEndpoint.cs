using FastEndpoints;
using Chatty.Api.Data;
using Chatty.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

public class AssignAgentRequest
{
    public string SessionId { get; set; } = default!;
    public string AgentName { get; set; } = default!;
}

public class AssignAgentEndpoint : Endpoint<AssignAgentRequest, string>
{
    private readonly ChatDbContext _db;

    public AssignAgentEndpoint(ChatDbContext db)
    {
        _db = db;
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
            await SendAsync($"Already handled by {existing.AgentName}");
            return;
        }

        _db.AgentSessions.Add(new AgentSession
        {
            SessionId = req.SessionId,
            AgentName = req.AgentName
        });

        await _db.SaveChangesAsync(ct);
        await SendAsync("Assigned");
    }
}