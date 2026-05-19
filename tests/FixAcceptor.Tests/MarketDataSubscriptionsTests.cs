using QuickFix;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class MarketDataSubscriptionsTests
{
    [Fact]
    public void Add_StoresSubscriptionByMDReqID()
    {
        var subs = new MarketDataSubscriptions();
        var sub = new MarketDataSubscription("R1", Session("C1"), new[] { "AAPL" });

        subs.Add(sub);

        Assert.Single(subs.All());
    }

    [Fact]
    public void Add_SameMDReqID_Overwrites()
    {
        var subs = new MarketDataSubscriptions();
        subs.Add(new MarketDataSubscription("R1", Session("C1"), new[] { "AAPL" }));
        subs.Add(new MarketDataSubscription("R1", Session("C1"), new[] { "MSFT" }));

        var only = Assert.Single(subs.All());
        Assert.Equal(new[] { "MSFT" }, only.Symbols);
    }

    [Fact]
    public void Remove_KnownMDReqID_ReturnsTrue()
    {
        var subs = new MarketDataSubscriptions();
        subs.Add(new MarketDataSubscription("R1", Session("C1"), new[] { "AAPL" }));

        Assert.True(subs.Remove("R1"));
        Assert.Empty(subs.All());
    }

    [Fact]
    public void Remove_UnknownMDReqID_ReturnsFalse()
    {
        var subs = new MarketDataSubscriptions();
        Assert.False(subs.Remove("nope"));
    }

    [Fact]
    public void RemoveAllForSession_OnlyRemovesMatchingSession()
    {
        var subs = new MarketDataSubscriptions();
        subs.Add(new MarketDataSubscription("R1", Session("C1"), new[] { "AAPL" }));
        subs.Add(new MarketDataSubscription("R2", Session("C1"), new[] { "MSFT" }));
        subs.Add(new MarketDataSubscription("R3", Session("C2"), new[] { "TSLA" }));

        var removed = subs.RemoveAllForSession(Session("C1"));

        Assert.Equal(2, removed);
        var remaining = Assert.Single(subs.All());
        Assert.Equal("R3", remaining.MDReqID);
    }

    private static SessionID Session(string target) => new("FIX.4.4", "SERVER", target);
}
