using Microsoft.Extensions.Logging;
using QuickFix;
using FixAcceptor.Services;

namespace FixAcceptor.Fix;

public class FixServerApp : MessageCracker, IApplication
{
    private readonly IFixMessageHandler _handler;
    private readonly ISessionRegistry _sessionRegistry;
    private readonly ILogger<FixServerApp> _logger;

    public FixServerApp(
        IFixMessageHandler handler,
        ISessionRegistry sessionRegistry,
        ILogger<FixServerApp> logger)
    {
        _handler = handler;
        _sessionRegistry = sessionRegistry;
        _logger = logger;
    }

    public void OnCreate(SessionID sessionId)
    {
        _logger.LogInformation("Session created: {SessionId}", sessionId);
    }

    public void OnLogon(SessionID sessionId)
    {
        _logger.LogInformation("Logon: {SessionId}", sessionId);
        _sessionRegistry.Register(sessionId);
    }

    public void OnLogout(SessionID sessionId)
    {
        _logger.LogInformation("Logout: {SessionId}", sessionId);
        _sessionRegistry.Unregister(sessionId);
    }

    public void ToAdmin(Message message, SessionID sessionId)
    {
        var msgType = message.Header.GetString(QuickFix.Fields.Tags.MsgType);
        _logger.LogDebug("ToAdmin [{MsgType}]: {SessionId}", msgType, sessionId);
    }

    public void FromAdmin(Message message, SessionID sessionId)
    {
        var msgType = message.Header.GetString(QuickFix.Fields.Tags.MsgType);
        _logger.LogDebug("FromAdmin [{MsgType}]: {SessionId}", msgType, sessionId);
    }

    public void ToApp(Message message, SessionID sessionId)
    {
        var msgType = message.Header.GetString(QuickFix.Fields.Tags.MsgType);
        _logger.LogInformation("ToApp [{MsgType}]: {SessionId}", msgType, sessionId);
    }

    public void FromApp(Message message, SessionID sessionId)
    {
        var msgType = message.Header.GetString(QuickFix.Fields.Tags.MsgType);
        _logger.LogInformation("FromApp [{MsgType}]: {SessionId}", msgType, sessionId);
        Crack(message, sessionId);
    }

    public void OnMessage(QuickFix.FIX44.NewOrderSingle order, SessionID sessionId)
    {
        _handler.HandleNewOrderSingle(order, sessionId);
    }
}
