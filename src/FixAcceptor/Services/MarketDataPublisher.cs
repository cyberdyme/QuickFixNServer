using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixAcceptor.Services;

/// <summary>
/// Publishes MarketDataIncrementalRefresh ticks for every active
/// snapshot-plus-update subscription. Stays idle when nothing is subscribed,
/// so the sample server is quiet by default.
/// </summary>
public class MarketDataPublisher : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private const decimal MaxWalkStep = 0.10m;

    private readonly IMarketDataSubscriptions _subscriptions;
    private readonly ISecurityUniverse _universe;
    private readonly IFixMessageSender _sender;
    private readonly ILogger<MarketDataPublisher> _logger;
    private readonly Random _rng = new(42);
    private readonly Dictionary<string, decimal> _lastMid = new(StringComparer.OrdinalIgnoreCase);

    public MarketDataPublisher(
        IMarketDataSubscriptions subscriptions,
        ISecurityUniverse universe,
        IFixMessageSender sender,
        ILogger<MarketDataPublisher> logger)
    {
        _subscriptions = subscriptions;
        _universe = universe;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataPublisher started — tick interval {Interval}", TickInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                PublishOneTick();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarketDataPublisher tick failed");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("MarketDataPublisher stopped");
    }

    private void PublishOneTick()
    {
        var subs = _subscriptions.All();
        if (subs.Count == 0) return;

        foreach (var sub in subs)
        {
            foreach (var symbol in sub.Symbols)
            {
                if (!_universe.Knows(symbol)) continue;
                var price = NextPrice(symbol);
                var inc = MarketDataBuilder.IncrementalRefresh(sub.MDReqID, symbol, price);
                try
                {
                    _sender.SendToTarget(inc, sub.SessionID);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "MarketDataIncrementalRefresh send failed for {SessionId} {Symbol}",
                        sub.SessionID, symbol);
                }
            }
        }
    }

    private decimal NextPrice(string symbol)
    {
        if (!_lastMid.TryGetValue(symbol, out var current))
            current = _universe.GetMidPrice(symbol);

        // Symmetric random walk capped at +/- MaxWalkStep so prices drift but
        // don't run away during a long demo session.
        var stepCents = (decimal)(_rng.NextDouble() * (double)(2m * MaxWalkStep)) - MaxWalkStep;
        var next = Math.Max(0.01m, decimal.Round(current + stepCents, 2));
        _lastMid[symbol] = next;
        return next;
    }
}
