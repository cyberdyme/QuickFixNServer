using System.Collections.Concurrent;

namespace FixAcceptor.Services;

public enum OrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Canceled,
    Replaced,
    Rejected
}

public record OrderState(
    string ClOrdID,
    string OrderID,
    string Symbol,
    char Side,
    decimal OrderQty,
    decimal Price,
    char OrdType,
    OrderStatus Status,
    decimal CumQty,
    decimal LeavesQty);

public interface IOrderBook
{
    OrderState Add(string clOrdId, string symbol, char side, decimal orderQty, decimal price, char ordType);
    OrderState? Get(string clOrdId);
    OrderState? Cancel(string origClOrdId);
    OrderState? Replace(string origClOrdId, string newClOrdId, decimal newQty, decimal newPrice);
}

public class OrderBook : IOrderBook
{
    private readonly ConcurrentDictionary<string, OrderState> _orders = new();

    public OrderState Add(string clOrdId, string symbol, char side, decimal orderQty, decimal price, char ordType)
    {
        var order = new OrderState(
            ClOrdID: clOrdId,
            OrderID: Guid.NewGuid().ToString("N"),
            Symbol: symbol,
            Side: side,
            OrderQty: orderQty,
            Price: price,
            OrdType: ordType,
            Status: OrderStatus.New,
            CumQty: 0m,
            LeavesQty: orderQty);

        if (!_orders.TryAdd(clOrdId, order))
            throw new InvalidOperationException($"Duplicate ClOrdID: {clOrdId}");

        return order;
    }

    public OrderState? Get(string clOrdId) =>
        _orders.TryGetValue(clOrdId, out var o) ? o : null;

    public OrderState? Cancel(string origClOrdId)
    {
        if (!_orders.TryGetValue(origClOrdId, out var existing))
            return null;
        if (!IsCancelable(existing.Status))
            return null;

        var canceled = existing with
        {
            Status = OrderStatus.Canceled,
            LeavesQty = 0m
        };
        _orders[origClOrdId] = canceled;
        return canceled;
    }

    public OrderState? Replace(string origClOrdId, string newClOrdId, decimal newQty, decimal newPrice)
    {
        if (!_orders.TryGetValue(origClOrdId, out var existing))
            return null;
        if (!IsCancelable(existing.Status))
            return null;
        if (_orders.ContainsKey(newClOrdId))
            return null;

        var replaced = existing with
        {
            ClOrdID = newClOrdId,
            OrderQty = newQty,
            Price = newPrice,
            Status = OrderStatus.Replaced,
            LeavesQty = newQty - existing.CumQty
        };

        // Index under the new ClOrdID; FIX convention is that the new ClOrdID
        // is the order's identity going forward.
        _orders.TryRemove(origClOrdId, out _);
        _orders[newClOrdId] = replaced;
        return replaced;
    }

    private static bool IsCancelable(OrderStatus status) =>
        status == OrderStatus.New || status == OrderStatus.PartiallyFilled;
}
