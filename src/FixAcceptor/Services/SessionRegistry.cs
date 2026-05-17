using System.Collections.Concurrent;
using QuickFix;

namespace FixAcceptor.Services;

public class SessionRegistry : ISessionRegistry
{
    private readonly ConcurrentDictionary<string, SessionID> _sessions = new();

    public void Register(SessionID sessionId)
    {
        var key = sessionId.ToString();
        _sessions.TryAdd(key, sessionId);
    }

    public void Unregister(SessionID sessionId)
    {
        var key = sessionId.ToString();
        _sessions.TryRemove(key, out _);
    }

    public IReadOnlyCollection<SessionID> GetActiveSessions()
    {
        return _sessions.Values.ToList();
    }

    public bool HasActiveSessions()
    {
        return !_sessions.IsEmpty;
    }
}
