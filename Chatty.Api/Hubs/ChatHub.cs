using Chatty.Api.Contracts;
using Chatty.Api.Data.Entities;
using Chatty.Api.Services;
using Microsoft.AspNetCore.SignalR;
using System.Data;
using System.Net;

namespace Chatty.Api.Hubs;

public class ChatHub : Hub
{
    private readonly IChatMessageQueue _queue;
    private readonly IAgentSessionTracker _agentTracker;
    private static readonly List<string> SessionOrder = new();
    private static readonly Dictionary<string, string> SessionIpMap = new();
    private static readonly Dictionary<string, string> CustomerConnections = new();

    public ChatHub(IChatMessageQueue queue, IAgentSessionTracker agentTracker)
    {
        _queue = queue;
        _agentTracker = agentTracker;
        _agentTracker = agentTracker;
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
        var role = Context.GetHttpContext()?.Request.Query["role"].ToString();

        if (role == "agent")
        {
            List<string> orphanedSessions = new();

            lock (_agentTracker.AgentSessions)
            {
                if (_agentTracker.AgentSessions.TryGetValue(Context.ConnectionId, out var sessions))
                {
                    // Collect sessions only assigned to this disconnected agent
                    foreach (var sessionId in sessions)
                    {
                        bool stillAssignedElsewhere = _agentTracker.AgentSessions
                            .Any(pair => pair.Key != Context.ConnectionId && pair.Value.Contains(sessionId));

                        if (!stillAssignedElsewhere)
                            orphanedSessions.Add(sessionId);
                    }

                    _agentTracker.AgentSessions.TryRemove(Context.ConnectionId ,out _);
                }
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "agents");

            foreach (var sessionId in orphanedSessions)
            {
                await Clients.Group("agents").SendAsync("SessionEnded", sessionId);
            }
        }
        else
        {
            string? sessionId = null;
            lock (CustomerConnections)
            {
                if (CustomerConnections.TryGetValue(Context.ConnectionId, out sessionId))
                    CustomerConnections.Remove(Context.ConnectionId);
            }

            if (sessionId != null)
            {
                // Optionally remove session from IP map and order tracking
                SessionIpMap.Remove(sessionId);
                SessionOrder.Remove(sessionId);
                await Clients.Group("agents").SendAsync("SessionEnded", sessionId);
            }
        }

        Console.WriteLine($"Disconnected: {Context.ConnectionId}, role: {role}");

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendTypingText(string sessionId, string user, string text)
    {
        var targets = _agentTracker.AgentSessions
            .Where(pair => pair.Value.Contains(sessionId))
            .Select(pair => pair.Key);

        foreach (var connId in targets)
        {
            await Clients.Client(connId).SendAsync("ReceiveTypingText", sessionId, user, text);
        }

        // Optionally also broadcast to the other side of the chat session (e.g. customer)
        await Clients.GroupExcept(sessionId, Context.ConnectionId)
            .SendAsync("ReceiveTypingText", sessionId, user, text);
    }

    public async Task NotifyAgentNewSession(string sessionId, string label, string ipAddress)
    {
        await Clients.Group("agents").SendAsync("NewSessionStarted", sessionId, label, ipAddress);
    }

    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

        var role = Context.GetHttpContext()?.Request.Query["role"].ToString();

        if (role == "agent")
        {
            lock (_agentTracker.AgentSessions)
            {
                if (!_agentTracker.AgentSessions.ContainsKey(Context.ConnectionId))
                    _agentTracker.AgentSessions[Context.ConnectionId] = new HashSet<string>();

                _agentTracker.AgentSessions[Context.ConnectionId].Add(sessionId);
            }
        }
        else
        {
            lock (CustomerConnections)
            {
                CustomerConnections[Context.ConnectionId] = sessionId;
            }
        }
    }

    public async Task SendMessage(string sessionId, string user, string senderRole, string message)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(message))
            return;

        // Cache the IP address only once per session
        if (!SessionIpMap.ContainsKey(sessionId))
        {
            var ipRaw = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            var forwarded = Context.GetHttpContext()?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var finalIp = !string.IsNullOrWhiteSpace(forwarded) ? forwarded : ipRaw;

            if (!string.IsNullOrWhiteSpace(finalIp))
                SessionIpMap[sessionId] = finalIp!;
        }

        var chatMessage = new ChatMessage
        {
            SessionId = sessionId,
            User = user,
            SenderRole = senderRole,
            Message = message,
            SentAt = DateTime.UtcNow,
            IpAddress = SessionIpMap.GetValueOrDefault(sessionId)
        };

        _queue.Enqueue(chatMessage);

        // Broadcast to everyone in the session group
        await Clients.Group(sessionId).SendAsync("ReceiveMessage", user, senderRole, message, sessionId);

        // If this is a new customer session, notify agent — but only if no agent already has this session
        if (senderRole == "customer" && message.Contains("[System] Chat started"))
        {
            bool hasActiveAgent = _agentTracker.AgentSessions.Any(pair => pair.Value.Contains(sessionId));
            if (!hasActiveAgent)
            {
                var label = GenerateSessionLabel(sessionId);
                await NotifyAgentNewSession(sessionId, label, SessionIpMap.GetValueOrDefault(sessionId)!);
            }
        }
    }


    private string GenerateSessionLabel(string sessionId)
    {
        if (!SessionOrder.Contains(sessionId))
            SessionOrder.Add(sessionId);

        return $"User {SessionOrder.IndexOf(sessionId) + 1}";
    }
}
