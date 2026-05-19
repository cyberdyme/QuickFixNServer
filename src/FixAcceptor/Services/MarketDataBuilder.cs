using QuickFix.Fields;

namespace FixAcceptor.Services;

public static class MarketDataBuilder
{
    private const decimal DefaultSpread = 0.05m;
    private const decimal DefaultSize = 1000m;

    /// <summary>
    /// Builds a MarketDataSnapshotFullRefresh (35=W) with a single best-bid and
    /// best-offer entry symmetrically priced around <paramref name="midPrice"/>.
    /// </summary>
    public static QuickFix.FIX44.MarketDataSnapshotFullRefresh Snapshot(
        string mdReqId, string symbol, decimal midPrice)
    {
        var msg = new QuickFix.FIX44.MarketDataSnapshotFullRefresh();
        msg.Set(new MDReqID(mdReqId));
        msg.Set(new Symbol(symbol));

        var bid = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
        bid.Set(new MDEntryType(MDEntryType.BID));
        bid.Set(new MDEntryPx(midPrice - DefaultSpread));
        bid.Set(new MDEntrySize(DefaultSize));
        msg.AddGroup(bid);

        var offer = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
        offer.Set(new MDEntryType(MDEntryType.OFFER));
        offer.Set(new MDEntryPx(midPrice + DefaultSpread));
        offer.Set(new MDEntrySize(DefaultSize));
        msg.AddGroup(offer);

        return msg;
    }

    /// <summary>
    /// Builds a MarketDataIncrementalRefresh (35=X) carrying one bid update and
    /// one offer update. Sent periodically while a subscription is active.
    /// </summary>
    public static QuickFix.FIX44.MarketDataIncrementalRefresh IncrementalRefresh(
        string mdReqId, string symbol, decimal midPrice)
    {
        var msg = new QuickFix.FIX44.MarketDataIncrementalRefresh();
        msg.Set(new MDReqID(mdReqId));

        var bid = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();
        bid.Set(new MDUpdateAction(MDUpdateAction.CHANGE));
        bid.Set(new MDEntryType(MDEntryType.BID));
        bid.Set(new Symbol(symbol));
        bid.Set(new MDEntryPx(midPrice - DefaultSpread));
        bid.Set(new MDEntrySize(DefaultSize));
        msg.AddGroup(bid);

        var offer = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();
        offer.Set(new MDUpdateAction(MDUpdateAction.CHANGE));
        offer.Set(new MDEntryType(MDEntryType.OFFER));
        offer.Set(new Symbol(symbol));
        offer.Set(new MDEntryPx(midPrice + DefaultSpread));
        offer.Set(new MDEntrySize(DefaultSize));
        msg.AddGroup(offer);

        return msg;
    }

    /// <summary>
    /// Builds a MarketDataRequestReject (35=Y) for an MD request we cannot serve
    /// (unknown symbol, malformed request, etc.).
    /// </summary>
    public static QuickFix.FIX44.MarketDataRequestReject Reject(
        string mdReqId, char rejectReason, string text)
    {
        var msg = new QuickFix.FIX44.MarketDataRequestReject(new MDReqID(mdReqId));
        msg.Set(new MDReqRejReason(rejectReason));
        msg.Set(new Text(text));
        return msg;
    }

    /// <summary>
    /// Builds a SecurityList (35=y) response listing the supplied symbols.
    /// </summary>
    public static QuickFix.FIX44.SecurityList SecurityList(
        string requestId, IReadOnlyList<string> symbols)
    {
        var msg = new QuickFix.FIX44.SecurityList();
        msg.Set(new SecurityReqID(requestId));
        msg.Set(new SecurityResponseID(Guid.NewGuid().ToString("N")));
        msg.Set(new SecurityRequestResult(SecurityRequestResult.VALID_REQUEST));

        foreach (var sym in symbols)
        {
            var grp = new QuickFix.FIX44.SecurityList.NoRelatedSymGroup();
            grp.Set(new Symbol(sym));
            msg.AddGroup(grp);
        }

        return msg;
    }

    /// <summary>
    /// Builds a TradingSessionStatus (35=h) response.
    /// </summary>
    public static QuickFix.FIX44.TradingSessionStatus TradingSessionStatus(
        string requestId, string tradingSessionId, int status)
    {
        var msg = new QuickFix.FIX44.TradingSessionStatus(
            new TradingSessionID(tradingSessionId),
            new TradSesStatus(status));
        msg.Set(new TradSesReqID(requestId));
        return msg;
    }
}
