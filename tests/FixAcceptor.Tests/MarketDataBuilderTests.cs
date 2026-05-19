using QuickFix.Fields;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class MarketDataBuilderTests
{
    [Fact]
    public void Snapshot_ContainsBidAndOfferEntries()
    {
        var msg = MarketDataBuilder.Snapshot("REQ1", "AAPL", midPrice: 100m);

        Assert.Equal("REQ1", msg.MDReqID.Value);
        Assert.Equal("AAPL", msg.Symbol.Value);

        var noEntries = msg.NoMDEntries.Value;
        Assert.Equal(2, noEntries);

        var entryTypes = new List<char>();
        var entryPrices = new List<decimal>();
        var grp = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
        for (var i = 1; i <= noEntries; i++)
        {
            msg.GetGroup(i, grp);
            entryTypes.Add(grp.MDEntryType.Value);
            entryPrices.Add(grp.MDEntryPx.Value);
        }

        Assert.Contains(MDEntryType.BID, entryTypes);
        Assert.Contains(MDEntryType.OFFER, entryTypes);
        Assert.All(entryPrices, p => Assert.True(p > 0m));
    }

    [Fact]
    public void IncrementalRefresh_HasMDUpdateActionPerEntry()
    {
        var msg = MarketDataBuilder.IncrementalRefresh("REQ1", "MSFT", midPrice: 200m);

        var noEntries = msg.NoMDEntries.Value;
        Assert.Equal(2, noEntries);

        var grp = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();
        for (var i = 1; i <= noEntries; i++)
        {
            msg.GetGroup(i, grp);
            Assert.True(grp.IsSetField(Tags.MDUpdateAction), "MDUpdateAction must be set on each entry");
            Assert.Equal("MSFT", grp.Symbol.Value);
        }
    }

    [Fact]
    public void Reject_SetsReasonAndText()
    {
        var msg = MarketDataBuilder.Reject("REQ1", MDReqRejReason.UNKNOWN_SYMBOL, "Unknown symbol: ZZZ");

        Assert.Equal("REQ1", msg.MDReqID.Value);
        Assert.Equal(MDReqRejReason.UNKNOWN_SYMBOL, msg.MDReqRejReason.Value);
        Assert.Contains("ZZZ", msg.Text.Value);
    }

    [Fact]
    public void SecurityList_ContainsRequestedSymbols()
    {
        var symbols = new[] { "AAPL", "MSFT", "TEST.A" };
        var msg = MarketDataBuilder.SecurityList("LIST1", symbols);

        Assert.Equal("LIST1", msg.SecurityReqID.Value);
        Assert.Equal(SecurityRequestResult.VALID_REQUEST, msg.SecurityRequestResult.Value);

        var noSym = msg.NoRelatedSym.Value;
        Assert.Equal(symbols.Length, noSym);

        var emitted = new List<string>();
        var grp = new QuickFix.FIX44.SecurityList.NoRelatedSymGroup();
        for (var i = 1; i <= noSym; i++)
        {
            msg.GetGroup(i, grp);
            emitted.Add(grp.Symbol.Value);
        }

        Assert.Equal(symbols, emitted);
    }

    [Fact]
    public void TradingSessionStatus_SetsCoreFields()
    {
        var msg = MarketDataBuilder.TradingSessionStatus("TSS1", "DAY", TradSesStatus.OPEN);

        Assert.Equal("TSS1", msg.TradSesReqID.Value);
        Assert.Equal("DAY", msg.TradingSessionID.Value);
        Assert.Equal(TradSesStatus.OPEN, msg.TradSesStatus.Value);
    }
}
