using Microsoft.Extensions.Logging.Abstractions;
using QuickFix;
using QuickFix.Fields;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class MarketDataHandlerTests
{
    private readonly SecurityUniverse _universe = new();
    private readonly MarketDataSubscriptions _subs = new();
    private readonly RecordingSender _sender = new();
    private readonly MarketDataHandler _handler;
    private readonly SessionID _sessionId = new("FIX.4.4", "SERVER", "CLIENT");

    public MarketDataHandlerTests()
    {
        _handler = new MarketDataHandler(_universe, _subs, _sender, NullLogger<MarketDataHandler>.Instance);
    }

    [Fact]
    public void MarketDataRequest_Snapshot_SendsOneSnapshotPerSymbol()
    {
        var request = BuildMdRequest("R1", subType: '0', symbols: new[] { "AAPL", "MSFT" });

        _handler.HandleMarketDataRequest(request, _sessionId);

        Assert.Equal(2, _sender.Sent.Count);
        Assert.All(_sender.Sent, s =>
            Assert.IsType<QuickFix.FIX44.MarketDataSnapshotFullRefresh>(s.message));
        Assert.Empty(_subs.All());
    }

    [Fact]
    public void MarketDataRequest_SnapshotPlusUpdates_SubscribesAndSendsSnapshot()
    {
        var request = BuildMdRequest("R1", subType: '1', symbols: new[] { "AAPL" });

        _handler.HandleMarketDataRequest(request, _sessionId);

        Assert.Single(_sender.Sent);
        var stored = Assert.Single(_subs.All());
        Assert.Equal("R1", stored.MDReqID);
        Assert.Equal(new[] { "AAPL" }, stored.Symbols);
    }

    [Fact]
    public void MarketDataRequest_Unsubscribe_RemovesSubscriptionAndSendsNothing()
    {
        _subs.Add(new MarketDataSubscription("R1", _sessionId, new[] { "AAPL" }));
        var request = BuildMdRequest("R1", subType: '2', symbols: new[] { "AAPL" });

        _handler.HandleMarketDataRequest(request, _sessionId);

        Assert.Empty(_subs.All());
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public void MarketDataRequest_UnknownSymbol_SendsReject()
    {
        var request = BuildMdRequest("R1", subType: '0', symbols: new[] { "ZZZ" });

        _handler.HandleMarketDataRequest(request, _sessionId);

        var reject = Assert.IsType<QuickFix.FIX44.MarketDataRequestReject>(_sender.Sent.Single().message);
        Assert.Equal("R1", reject.MDReqID.Value);
        Assert.Equal(MDReqRejReason.UNKNOWN_SYMBOL, reject.MDReqRejReason.Value);
    }

    [Fact]
    public void MarketDataRequest_UnsupportedSubType_SendsReject()
    {
        var request = BuildMdRequest("R1", subType: '9', symbols: new[] { "AAPL" });

        _handler.HandleMarketDataRequest(request, _sessionId);

        var reject = Assert.IsType<QuickFix.FIX44.MarketDataRequestReject>(_sender.Sent.Single().message);
        Assert.Equal(MDReqRejReason.UNSUPPORTED_SUBSCRIPTIONREQUESTTYPE, reject.MDReqRejReason.Value);
    }

    [Fact]
    public void SecurityListRequest_SendsListOfAllUniverseSymbols()
    {
        var request = new QuickFix.FIX44.SecurityListRequest(
            new SecurityReqID("LIST1"),
            new SecurityListRequestType(SecurityListRequestType.SYMBOL));

        _handler.HandleSecurityListRequest(request, _sessionId);

        var list = Assert.IsType<QuickFix.FIX44.SecurityList>(_sender.Sent.Single().message);
        Assert.Equal("LIST1", list.SecurityReqID.Value);
        Assert.Equal(_universe.AllSymbols().Count, list.NoRelatedSym.Value);
    }

    [Fact]
    public void TradingSessionStatusRequest_SendsOpenStatus()
    {
        var request = new QuickFix.FIX44.TradingSessionStatusRequest(
            new TradSesReqID("TSS1"),
            new SubscriptionRequestType('0'));

        _handler.HandleTradingSessionStatusRequest(request, _sessionId);

        var status = Assert.IsType<QuickFix.FIX44.TradingSessionStatus>(_sender.Sent.Single().message);
        Assert.Equal("TSS1", status.TradSesReqID.Value);
        Assert.Equal(TradSesStatus.OPEN, status.TradSesStatus.Value);
    }

    private static QuickFix.FIX44.MarketDataRequest BuildMdRequest(
        string mdReqId, char subType, IEnumerable<string> symbols)
    {
        var request = new QuickFix.FIX44.MarketDataRequest(
            new MDReqID(mdReqId),
            new SubscriptionRequestType(subType),
            new MarketDepth(1));
        request.Set(new NoMDEntryTypes(1));
        var entryGroup = new QuickFix.FIX44.MarketDataRequest.NoMDEntryTypesGroup();
        entryGroup.Set(new MDEntryType(MDEntryType.BID));
        request.AddGroup(entryGroup);

        foreach (var sym in symbols)
        {
            var symGroup = new QuickFix.FIX44.MarketDataRequest.NoRelatedSymGroup();
            symGroup.Set(new Symbol(sym));
            request.AddGroup(symGroup);
        }
        return request;
    }

    private class RecordingSender : IFixMessageSender
    {
        public List<(Message message, SessionID sessionId)> Sent { get; } = new();
        public bool SendToTarget(Message message, SessionID sessionId)
        {
            Sent.Add((message, sessionId));
            return true;
        }
    }
}
