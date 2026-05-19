using System.Collections.Concurrent;
using QuickFix;

namespace FixAcceptor.Services;

public record MarketDataSubscription(
    string MDReqID,
    SessionID SessionID,
    IReadOnlyList<string> Symbols);

public interface IMarketDataSubscriptions
{
    void Add(MarketDataSubscription subscription);
    bool Remove(string mdReqId);
    int RemoveAllForSession(SessionID sessionId);
    IReadOnlyCollection<MarketDataSubscription> All();
}

public class MarketDataSubscriptions : IMarketDataSubscriptions
{
    private readonly ConcurrentDictionary<string, MarketDataSubscription> _subs = new();

    public void Add(MarketDataSubscription subscription) =>
        _subs[subscription.MDReqID] = subscription;

    public bool Remove(string mdReqId) => _subs.TryRemove(mdReqId, out _);

    public int RemoveAllForSession(SessionID sessionId)
    {
        var sessionKey = sessionId.ToString();
        var stale = _subs
            .Where(kv => kv.Value.SessionID.ToString() == sessionKey)
            .Select(kv => kv.Key)
            .ToArray();
        var removed = 0;
        foreach (var key in stale)
            if (_subs.TryRemove(key, out _)) removed++;
        return removed;
    }

    public IReadOnlyCollection<MarketDataSubscription> All() => _subs.Values.ToList();
}
