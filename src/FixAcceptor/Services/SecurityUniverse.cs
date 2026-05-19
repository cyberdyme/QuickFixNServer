namespace FixAcceptor.Services;

public interface ISecurityUniverse
{
    bool Knows(string symbol);
    IReadOnlyList<string> AllSymbols();
    decimal GetMidPrice(string symbol);
}

/// <summary>
/// Static instrument universe used by the sample market-data responder.
/// Real venues would back this with a security master DB; here we ship a
/// fixed table with deterministic seed prices so the demo is reproducible.
/// </summary>
public class SecurityUniverse : ISecurityUniverse
{
    private static readonly IReadOnlyDictionary<string, decimal> Seeds =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["AAPL"] = 175m,
            ["MSFT"] = 350m,
            ["GOOG"] = 140m,
            ["TSLA"] = 220m,
            ["TEST.A"] = 100m,
        };

    public bool Knows(string symbol) => Seeds.ContainsKey(symbol);

    public IReadOnlyList<string> AllSymbols() => Seeds.Keys.ToList();

    public decimal GetMidPrice(string symbol) =>
        Seeds.TryGetValue(symbol, out var p)
            ? p
            : throw new KeyNotFoundException($"Unknown symbol: {symbol}");
}
