using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using FixAcceptor.Services;

namespace FixAcceptor.Fix;

public class FixServerApp : MessageCracker, IApplication
{
    private readonly IFixMessageHandler _handler;
    private readonly IMarketDataHandler _marketDataHandler;
    private readonly IMarketDataSubscriptions _marketDataSubscriptions;
    private readonly ISessionRegistry _sessionRegistry;
    private readonly ILogger<FixServerApp> _logger;

    public FixServerApp(
        IFixMessageHandler handler,
        IMarketDataHandler marketDataHandler,
        IMarketDataSubscriptions marketDataSubscriptions,
        ISessionRegistry sessionRegistry,
        ILogger<FixServerApp> logger)
    {
        _handler = handler;
        _marketDataHandler = marketDataHandler;
        _marketDataSubscriptions = marketDataSubscriptions;
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
        var removed = _marketDataSubscriptions.RemoveAllForSession(sessionId);
        if (removed > 0)
        {
            _logger.LogInformation(
                "Cleared {Count} market-data subscription(s) for {SessionId}",
                removed, sessionId);
        }
    }

    public void ToAdmin(Message message, SessionID sessionId)
    {
        var msgType = SafeGetMsgType(message);
        _logger.LogDebug("ToAdmin [{MsgType}]: {SessionId}", msgType, sessionId);
    }

    public void FromAdmin(Message message, SessionID sessionId)
    {
        var msgType = SafeGetMsgType(message);
        switch (msgType)
        {
            case AdminMsgType.Logon:
                LogInboundLogon(message, sessionId);
                break;
            case AdminMsgType.Logout:
                LogInboundLogout(message, sessionId);
                break;
            case AdminMsgType.Heartbeat:
                _logger.LogTrace("Heartbeat from {SessionId}", sessionId);
                break;
            case AdminMsgType.TestRequest:
                _logger.LogDebug(
                    "TestRequest from {SessionId} TestReqID={TestReqId}",
                    sessionId, OptionalString(message, Tags.TestReqID));
                break;
            case AdminMsgType.ResendRequest:
                _logger.LogInformation(
                    "ResendRequest from {SessionId} BeginSeqNo={Begin} EndSeqNo={End}",
                    sessionId,
                    OptionalString(message, Tags.BeginSeqNo),
                    OptionalString(message, Tags.EndSeqNo));
                break;
            case AdminMsgType.SequenceReset:
                _logger.LogInformation(
                    "SequenceReset from {SessionId} GapFillFlag={GapFill} NewSeqNo={NewSeqNo}",
                    sessionId,
                    OptionalString(message, Tags.GapFillFlag),
                    OptionalString(message, Tags.NewSeqNo));
                break;
            case AdminMsgType.Reject:
                _logger.LogWarning(
                    "Session-level Reject from {SessionId}: RefSeqNum={RefSeqNum} Text={Text}",
                    sessionId,
                    OptionalString(message, Tags.RefSeqNum),
                    OptionalString(message, Tags.Text));
                break;
            default:
                _logger.LogDebug("FromAdmin [{MsgType}]: {SessionId}", msgType, sessionId);
                break;
        }
    }

    public void ToApp(Message message, SessionID sessionId)
    {
        var msgType = SafeGetMsgType(message);
        _logger.LogInformation("ToApp [{MsgType}]: {SessionId}", msgType, sessionId);
    }

    public void FromApp(Message message, SessionID sessionId)
    {
        var msgType = SafeGetMsgType(message);
        _logger.LogInformation("FromApp [{MsgType}]: {SessionId}", msgType, sessionId);
        try
        {
            Crack(message, sessionId);
        }
        catch (UnsupportedMessageType)
        {
            // Surface a clear log line; QuickFIX/n's session loop will send a
            // BusinessMessageReject (35=j) for us when we let this propagate.
            _logger.LogWarning(
                "Unsupported app message type {MsgType} from {SessionId} — BusinessMessageReject will be sent",
                msgType, sessionId);
            throw;
        }
    }

    public void OnMessage(QuickFix.FIX44.NewOrderSingle order, SessionID sessionId)
    {
        _handler.HandleNewOrderSingle(order, sessionId);
    }

    public void OnMessage(QuickFix.FIX44.OrderCancelRequest request, SessionID sessionId)
    {
        _handler.HandleOrderCancelRequest(request, sessionId);
    }

    public void OnMessage(QuickFix.FIX44.OrderCancelReplaceRequest request, SessionID sessionId)
    {
        _handler.HandleOrderCancelReplaceRequest(request, sessionId);
    }

    public void OnMessage(QuickFix.FIX44.OrderStatusRequest request, SessionID sessionId)
    {
        _handler.HandleOrderStatusRequest(request, sessionId);
    }

    public void OnMessage(QuickFix.FIX44.MarketDataRequest request, SessionID sessionId)
    {
        _marketDataHandler.HandleMarketDataRequest(request, sessionId);
    }

    public void OnMessage(QuickFix.FIX44.SecurityListRequest request, SessionID sessionId)
    {
        _marketDataHandler.HandleSecurityListRequest(request, sessionId);
    }

    public void OnMessage(QuickFix.FIX44.TradingSessionStatusRequest request, SessionID sessionId)
    {
        _marketDataHandler.HandleTradingSessionStatusRequest(request, sessionId);
    }

    private void LogInboundLogon(Message message, SessionID sessionId)
    {
        _logger.LogInformation(
            "Inbound Logon from {SessionId} EncryptMethod={EncryptMethod} HeartBtInt={HeartBtInt} ResetSeqNumFlag={ResetSeqNum}",
            sessionId,
            OptionalString(message, Tags.EncryptMethod),
            OptionalString(message, Tags.HeartBtInt),
            OptionalString(message, Tags.ResetSeqNumFlag));
    }

    private void LogInboundLogout(Message message, SessionID sessionId)
    {
        _logger.LogInformation(
            "Inbound Logout from {SessionId} Text={Text}",
            sessionId, OptionalString(message, Tags.Text));
    }

    private static string SafeGetMsgType(Message message)
    {
        try { return message.Header.GetString(Tags.MsgType); }
        catch { return "<unknown>"; }
    }

    private static string OptionalString(Message message, int tag)
    {
        try { return message.IsSetField(tag) ? message.GetString(tag) : ""; }
        catch { return ""; }
    }

    // FIX 4.4 admin message type codes — kept as a local table so we don't
    // depend on QuickFix.Fields.MsgType constants (whose names vary by version).
    private static class AdminMsgType
    {
        public const string Heartbeat = "0";
        public const string TestRequest = "1";
        public const string ResendRequest = "2";
        public const string Reject = "3";
        public const string SequenceReset = "4";
        public const string Logout = "5";
        public const string Logon = "A";
    }
}
