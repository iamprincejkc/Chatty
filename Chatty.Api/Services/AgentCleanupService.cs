using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Chatty.Api.Contracts;
using Chatty.Api.Hubs;
using System.Collections.Concurrent;

namespace Chatty.Api.Services;

public class AgentCleanupService : BackgroundService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IAgentSessionTracker _agentTracker;
    private static readonly ConcurrentDictionary<string, DateTime> LastPingTimestamps = new();

    public AgentCleanupService(IHubContext<ChatHub> hubContext, IAgentSessionTracker agentTracker)
    {
        _hubContext = hubContext;
        _agentTracker = agentTracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var timeout = TimeSpan.FromMinutes(2); // 👈 consider agent dead if no ping for 2 mins
            var ghostAgents = new List<string>();

            // 👁️ Loop through all tracked usernames
            foreach (var kvp in _agentTracker.AgentSessionsByUsername)
            {
                var username = kvp.Key;

                if (LastPingTimestamps.TryGetValue(username, out var lastSeen))
                {
                    if (now - lastSeen > timeout)
                    {
                        ghostAgents.Add(username);
                    }
                }
                else
                {
                    // If never pinged, assume it was a ghost from startup
                    ghostAgents.Add(username);
                }
            }

            // 👻 Remove ghost agents from memory
            foreach (var username in ghostAgents)
            {
                _agentTracker.AgentSessionsByUsername.TryRemove(username, out _);
                LastPingTimestamps.TryRemove(username, out _);
                Console.WriteLine($"[Cleanup] Removed ghost agent '{username}'");
            }

            // 🛰️ Ask all agents to report back (PingCheck is a no-op)
            await _hubContext.Clients.Group("agents").SendAsync("PingCheck");

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    // Called from ChatHub whenever agent responds
    public static void ReportAgentHeartbeat(string username)
    {
        LastPingTimestamps[username] = DateTime.UtcNow;
    }
}
