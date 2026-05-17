using QuickFix;

namespace FixAcceptor.Services;

public interface ISessionRegistry
{
    void Register(SessionID sessionId);
    void Unregister(SessionID sessionId);
    IReadOnlyCollection<SessionID> GetActiveSessions();
    bool HasActiveSessions();
}
