using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Chatty.Api.Hubs;
using Chatty.Api.Contracts;

namespace Chatty.Api.Services;

public class AgentCleanupService : BackgroundService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IAgentSessionTracker _agentTracker;

    public AgentCleanupService(IHubContext<ChatHub> hubContext, IAgentSessionTracker agentTracker)
    {
        _hubContext = hubContext;
        _agentTracker = agentTracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var deadAgents = new List<string>();

            foreach (var kvp in _agentTracker.AgentSessions)
            {
                var connId = kvp.Key;
                // Try sending a ping message or check if still connected
                try
                {
                    await _hubContext.Clients.Client(connId).SendAsync("Ping");
                }
                catch
                {
                    deadAgents.Add(connId);
                }
            }

            foreach (var connId in deadAgents)
            {
                _agentTracker.AgentSessions.TryRemove(connId, out _);
                Console.WriteLine($"[Cleanup] Removed ghost agent: {connId}");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
