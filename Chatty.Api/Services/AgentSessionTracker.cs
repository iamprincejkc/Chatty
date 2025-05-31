using Chatty.Api.Contracts;
using System.Collections.Concurrent;

namespace Chatty.Api.Services;

public class AgentSessionTracker: IAgentSessionTracker
{
    public ConcurrentDictionary<string, HashSet<string>> AgentSessionsByUsername { get; } = new();
    public ConcurrentDictionary<string, string> ConnectedAgentsByUsername { get; } = new();
}
