using Chatty.Api.Data.Entities;
using Chatty.Api.Data;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

public class GetChatBySessionEndpoint : Endpoint<GetChatBySessionEndpoint.Request, List<ChatMessage>>
{
    public class Request
    {
        public string SessionId { get; set; } = default!;
    }

    private readonly ChatDbContext _db;

    public GetChatBySessionEndpoint(ChatDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/chat/{SessionId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var messages = await _db.ChatMessages
            .Where(x => x.SessionId == req.SessionId)
            .OrderBy(x => x.SentAt)
            .ToListAsync(ct);

        await SendAsync(messages);
    }
}