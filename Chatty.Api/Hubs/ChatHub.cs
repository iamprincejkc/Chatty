using Chatty.Api.Data.Entities;
using Chatty.Api.Services;
using Microsoft.AspNetCore.SignalR;
using System.Data;
using System.Net;

namespace Chatty.Api.Hubs;

public class ChatHub : Hub
{
    private readonly IChatMessageQueue _queue; 
    private static readonly List<string> SessionOrder = new();

    public ChatHub(IChatMessageQueue queue)
    {
        _queue = queue;
    }

    public override async Task OnConnectedAsync()
    {
        var role = Context.GetHttpContext()?.Request.Query["role"].ToString();
        var isAgent = string.Equals(role, "agent", StringComparison.OrdinalIgnoreCase);
        if (isAgent)
            await Groups.AddToGroupAsync(Context.ConnectionId, "agents");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var httpContext = Context.GetHttpContext();
        var role = httpContext?.Request.Query["role"].ToString();

        if (role == "agent")
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "agents");
        }

        Console.WriteLine($"Disconnected: {Context.ConnectionId}, role: {role}");

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendTypingText(string sessionId, string user, string text)
    {
        await Clients.GroupExcept(sessionId, Context.ConnectionId).SendAsync("ReceiveTypingText", sessionId, user, text);
    }

    public async Task NotifyAgentNewSession(string sessionId, string label, string ipAddress)
    {
        await Clients.Group("agents").SendAsync("NewSessionStarted", sessionId, label, ipAddress);
    }

    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    }

    public async Task SendMessage(string sessionId, string user, string senderRole, string message)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(message))
            return;

        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

        if (ip == "::1")
            ip = Context.GetHttpContext()?.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "127.0.0.1"; //testing

        var chatMessage = new ChatMessage
        {
            SessionId = sessionId,
            User = user,
            SenderRole = senderRole,
            Message = message,
            SentAt = DateTime.UtcNow,
            IpAddress = ip
        };

        _queue.Enqueue(chatMessage);

        // Broadcast to all members of this session (agent + customer)
        await Clients.Group(sessionId).SendAsync("ReceiveMessage", user, senderRole, message);

        if (senderRole == "customer" && message.Contains("[System] Chat started"))
        {
            var label = GenerateSessionLabel(sessionId);
            await NotifyAgentNewSession(sessionId, label, ip!);
        }
    }

    private string GenerateSessionLabel(string sessionId)
    {
        if (!SessionOrder.Contains(sessionId))
            SessionOrder.Add(sessionId);

        return $"User {SessionOrder.IndexOf(sessionId) + 1}";
    }
}
