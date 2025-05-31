using System.Collections.Concurrent;

namespace Chatty.Api.Contracts;

public interface IAgentSessionTracker
{
    public ConcurrentDictionary<string, HashSet<string>> AgentSessions { get;}

}
