using Chatty.Api.Contracts;
using Chatty.Api.Data;
using Chatty.Api.Data.Entities;
using Chatty.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net;

namespace Chatty.Api.Hubs;

public class ChatHub : Hub
{
    private readonly IChatMessageQueue _queue;
    private readonly IAgentSessionTracker _agentTracker;
    private static readonly List<string> SessionOrder = new();
    private static readonly Dictionary<string, string> SessionIpMap = new();
    private static readonly Dictionary<string, string> CustomerConnections = new();
    private static readonly ConcurrentDictionary<string, string> ConnectedAgentsByUsername = new(); // username => connectionId
    private readonly IServiceScopeFactory _scopeFactory;
    public ChatHub(IChatMessageQueue queue, IAgentSessionTracker agentTracker, IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _agentTracker = agentTracker;
        _scopeFactory = scopeFactory;
    }

    public override async Task OnConnectedAsync()
    {
        var context = Context.GetHttpContext();
        var role = context?.Request.Query["role"].ToString();
        var username = context?.Request.Query["username"].ToString();
        var sessionId = context?.Request.Query["sessionId"].ToString();

        if (role == "agent" && !string.IsNullOrWhiteSpace(username))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "agents");
            ConnectedAgentsByUsername[username] = Context.ConnectionId;
            Console.WriteLine($"Agent connected: {username} ({Context.ConnectionId})");
        }

        // ✅ [NEW] Detect new customer session
        if (role == "customer" && !string.IsNullOrWhiteSpace(sessionId))
        {
            // Track session IP
            if (!SessionIpMap.ContainsKey(sessionId))
            {
                var ipRaw = context?.Connection.RemoteIpAddress?.ToString();
                var forwarded = context?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                var finalIp = !string.IsNullOrWhiteSpace(forwarded) ? forwarded : ipRaw;

                if (!string.IsNullOrWhiteSpace(finalIp))
                    SessionIpMap[sessionId] = finalIp!;
            }

            // If session is new
            if (!SessionOrder.Contains(sessionId))
            {
                SessionOrder.Add(sessionId);

                // Check if already assigned to agent (in-memory or DB)
                bool isHandled = _agentTracker.AgentSessionsByUsername.Any(pair =>
                    pair.Value.Contains(sessionId) && ConnectedAgentsByUsername.ContainsKey(pair.Key));

                if (!isHandled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                    var dbAssigned = await db.AgentSessions.AnyAsync(x => x.SessionId == sessionId);

                    if (!dbAssigned)
                    {
                        var label = GenerateSessionLabel(sessionId);
                        var ip = SessionIpMap.GetValueOrDefault(sessionId, "unknown");
                        await NotifyAgentNewSession(sessionId, label, ip);
                    }
                }
            }
        }

        await base.OnConnectedAsync();
    }


    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var role = Context.GetHttpContext()?.Request.Query["role"].ToString();
        var username = Context.GetHttpContext()?.Request.Query["username"].ToString();

        if (role == "agent" && !string.IsNullOrWhiteSpace(username))
        {
            List<string> orphanedSessions = new();

            if (_agentTracker.AgentSessionsByUsername.TryRemove(username, out var sessions))
            {
                lock (sessions)
                {
                    orphanedSessions.AddRange(sessions);
                }
            }

            ConnectedAgentsByUsername.TryRemove(username, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "agents");

            // ✅ Cleanup DB session assignments
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

            foreach (var sessionId in orphanedSessions)
            {
                var entity = await db.AgentSessions.FirstOrDefaultAsync(x => x.SessionId == sessionId);
                if (entity != null)
                {
                    db.AgentSessions.Remove(entity);
                    await Clients.Group("agents").SendAsync("SessionEnded", sessionId);
                }
            }

            await db.SaveChangesAsync();
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
        var targets = _agentTracker.AgentSessionsByUsername
            .Where(pair => pair.Value.Contains(sessionId))
            .Select(pair => pair.Key);

        foreach (var agentUsername in targets)
        {
            await Clients.Group("agents").SendAsync("ReceiveTypingText", sessionId, user, text);
        }

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
        var username = Context.GetHttpContext()?.Request.Query["username"].ToString();

        if (role == "agent" && !string.IsNullOrWhiteSpace(username))
        {
            // ✅ Track in-memory agent session
            var agentSessions = _agentTracker.AgentSessionsByUsername.GetOrAdd(username, _ => new HashSet<string>());
            lock (agentSessions)
            {
                agentSessions.Add(sessionId);
            }

            // ✅ Track active connection
            ConnectedAgentsByUsername[username] = Context.ConnectionId;

            // ✅ Ensure agent is in group
            await Groups.AddToGroupAsync(Context.ConnectionId, "agents");
        }
        else
        {
            // ✅ Track customer session
            lock (CustomerConnections)
            {
                CustomerConnections[Context.ConnectionId] = sessionId;
            }
        }
    }

    public Task PingCheck()
    {
        var username = Context.GetHttpContext()?.Request.Query["username"].ToString();
        if (!string.IsNullOrWhiteSpace(username))
        {
            AgentCleanupService.ReportAgentHeartbeat(username);
        }
        return Task.CompletedTask;
    }

    public async Task SendMessage(string sessionId, string user, string senderRole, string message)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(message))
            return;

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

        await Clients.Group(sessionId).SendAsync("ReceiveMessage", user, senderRole, message, sessionId);

        // ✅ Detect new customer session and notify agent UI
        if (senderRole == "customer" && message.Contains("[System] Chat started"))
        {
            var isFirstTime = !SessionOrder.Contains(sessionId);

            if (isFirstTime)
            {
                SessionOrder.Add(sessionId); // mark as initialized

                // double-check if someone is already assigned (live or stale DB)
                bool isHandled = _agentTracker.AgentSessionsByUsername.Any(pair =>
                    pair.Value.Contains(sessionId) && ConnectedAgentsByUsername.ContainsKey(pair.Key));

                if (!isHandled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                    var dbAssigned = await db.AgentSessions.AnyAsync(x => x.SessionId == sessionId);

                    if (!dbAssigned)
                    {
                        var label = GenerateSessionLabel(sessionId);
                        var ip = SessionIpMap.GetValueOrDefault(sessionId, "unknown");
                        await NotifyAgentNewSession(sessionId, label, ip);
                    }
                }
            }
        }
    }


    private string GenerateSessionLabel(string sessionId)
    {
        if (!SessionOrder.Contains(sessionId))
            SessionOrder.Add(sessionId);

        return $"New User {SessionOrder.IndexOf(sessionId) + 1}";
    }

    public static List<string> GetActiveCustomerSessions()
    {
        lock (CustomerConnections)
        {
            return CustomerConnections.Values.Distinct().ToList();
        }
    }
}
