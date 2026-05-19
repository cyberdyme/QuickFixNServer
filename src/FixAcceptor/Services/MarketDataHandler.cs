using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace FixAcceptor.Services;

public interface IMarketDataHandler
{
    void HandleMarketDataRequest(QuickFix.FIX44.MarketDataRequest request, SessionID sessionId);
    void HandleSecurityListRequest(QuickFix.FIX44.SecurityListRequest request, SessionID sessionId);
    void HandleTradingSessionStatusRequest(QuickFix.FIX44.TradingSessionStatusRequest request, SessionID sessionId);
}

public class MarketDataHandler : IMarketDataHandler
{
    // FIX 4.4 SubscriptionRequestType(263) values. Spelled out as char literals
    // because QuickFIX/n's generated SubscriptionRequestType constants for
    // values 0 and 2 have inconsistent names across versions.
    private const char SubTypeSnapshot = '0';
    private const char SubTypeSnapshotPlusUpdates = '1';
    private const char SubTypeUnsubscribe = '2';

    private const string TradingSessionId = "DAY";

    private readonly ISecurityUniverse _universe;
    private readonly IMarketDataSubscriptions _subscriptions;
    private readonly IFixMessageSender _sender;
    private readonly ILogger<MarketDataHandler> _logger;

    public MarketDataHandler(
        ISecurityUniverse universe,
        IMarketDataSubscriptions subscriptions,
        IFixMessageSender sender,
        ILogger<MarketDataHandler> logger)
    {
        _universe = universe;
        _subscriptions = subscriptions;
        _sender = sender;
        _logger = logger;
    }

    public void HandleMarketDataRequest(QuickFix.FIX44.MarketDataRequest request, SessionID sessionId)
    {
        var mdReqId = request.MDReqID.Value;
        var subType = request.SubscriptionRequestType.Value;

        var symbols = ExtractSymbols(request);
        _logger.LogInformation(
            "MarketDataRequest: MDReqID={ReqId} SubType={Sub} Symbols=[{Symbols}]",
            mdReqId, subType, string.Join(",", symbols));

        if (subType == SubTypeUnsubscribe)
        {
            var removed = _subscriptions.Remove(mdReqId);
            _logger.LogInformation(
                "Unsubscribe MDReqID={ReqId} removed={Removed}", mdReqId, removed);
            return;
        }

        if (subType != SubTypeSnapshot && subType != SubTypeSnapshotPlusUpdates)
        {
            var reject = MarketDataBuilder.Reject(
                mdReqId,
                MDReqRejReason.UNSUPPORTED_SUBSCRIPTIONREQUESTTYPE,
                $"Unsupported SubscriptionRequestType: {subType}");
            _sender.SendToTarget(reject, sessionId);
            return;
        }

        var unknown = symbols.Where(s => !_universe.Knows(s)).ToList();
        if (unknown.Count > 0)
        {
            var reject = MarketDataBuilder.Reject(
                mdReqId,
                MDReqRejReason.UNKNOWN_SYMBOL,
                $"Unknown symbol(s): {string.Join(",", unknown)}");
            _sender.SendToTarget(reject, sessionId);
            return;
        }

        foreach (var symbol in symbols)
        {
            var snapshot = MarketDataBuilder.Snapshot(mdReqId, symbol, _universe.GetMidPrice(symbol));
            _sender.SendToTarget(snapshot, sessionId);
        }

        if (subType == SubTypeSnapshotPlusUpdates)
        {
            _subscriptions.Add(new MarketDataSubscription(mdReqId, sessionId, symbols));
            _logger.LogInformation(
                "Subscribed MDReqID={ReqId} Symbols=[{Symbols}]", mdReqId, string.Join(",", symbols));
        }
    }

    public void HandleSecurityListRequest(QuickFix.FIX44.SecurityListRequest request, SessionID sessionId)
    {
        var requestId = request.SecurityReqID.Value;
        _logger.LogInformation("SecurityListRequest: SecurityReqID={ReqId}", requestId);

        var symbols = _universe.AllSymbols();
        var list = MarketDataBuilder.SecurityList(requestId, symbols);
        _sender.SendToTarget(list, sessionId);
    }

    public void HandleTradingSessionStatusRequest(
        QuickFix.FIX44.TradingSessionStatusRequest request, SessionID sessionId)
    {
        var requestId = request.TradSesReqID.Value;
        _logger.LogInformation("TradingSessionStatusRequest: TradSesReqID={ReqId}", requestId);

        var status = MarketDataBuilder.TradingSessionStatus(
            requestId, TradingSessionId, TradSesStatus.OPEN);
        _sender.SendToTarget(status, sessionId);
    }

    private static List<string> ExtractSymbols(QuickFix.FIX44.MarketDataRequest request)
    {
        var symbols = new List<string>();
        var noRelatedSym = request.NoRelatedSym.Value;
        var group = new QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup();
        for (var i = 1; i <= noRelatedSym; i++)
        {
            request.GetGroup(i, group);
            symbols.Add(group.Symbol.Value);
        }
        return symbols;
    }
}
