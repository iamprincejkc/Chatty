using Chatty.Api.Contracts;
using System.Collections.Concurrent;

namespace Chatty.Api.Services;

public class AgentSessionTracker: IAgentSessionTracker
{
    public ConcurrentDictionary<string, HashSet<string>> AgentSessions { get; } = new();
}
