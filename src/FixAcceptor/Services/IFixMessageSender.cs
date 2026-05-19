using QuickFix;

namespace FixAcceptor.Services;

public interface IFixMessageSender
{
    bool SendToTarget(Message message, SessionID sessionId);
}

public class QuickFixMessageSender : IFixMessageSender
{
    public bool SendToTarget(Message message, SessionID sessionId)
        => Session.SendToTarget(message, sessionId);
}
