using System.Collections.Concurrent;

namespace Chatty.Api.Contracts;

public interface IAgentSessionTracker
{
    ConcurrentDictionary<string, HashSet<string>> AgentSessionsByUsername { get; }
    ConcurrentDictionary<string, string> ConnectedAgentsByUsername { get; }

}
