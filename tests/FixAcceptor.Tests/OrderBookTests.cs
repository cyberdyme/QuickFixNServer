using QuickFix.Fields;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class OrderBookTests
{
    [Fact]
    public void Add_ReturnsNewOrderWithAssignedOrderID()
    {
        var book = new OrderBook();

        var order = book.Add("C1", "AAPL", Side.BUY, 100m, 0m, OrdType.MARKET);

        Assert.Equal("C1", order.ClOrdID);
        Assert.Equal(OrderStatus.New, order.Status);
        Assert.Equal(100m, order.LeavesQty);
        Assert.Equal(0m, order.CumQty);
        Assert.True(Guid.TryParseExact(order.OrderID, "N", out _));
    }

    [Fact]
    public void Add_DuplicateClOrdID_Throws()
    {
        var book = new OrderBook();
        book.Add("C1", "AAPL", Side.BUY, 100m, 0m, OrdType.MARKET);

        Assert.Throws<InvalidOperationException>(() =>
            book.Add("C1", "AAPL", Side.BUY, 50m, 0m, OrdType.MARKET));
    }

    [Fact]
    public void Get_ReturnsNullForUnknownClOrdID()
    {
        var book = new OrderBook();
        Assert.Null(book.Get("nope"));
    }

    [Fact]
    public void Cancel_KnownOrder_MarksCanceled()
    {
        var book = new OrderBook();
        book.Add("C1", "AAPL", Side.BUY, 100m, 0m, OrdType.MARKET);

        var canceled = book.Cancel("C1");

        Assert.NotNull(canceled);
        Assert.Equal(OrderStatus.Canceled, canceled!.Status);
        Assert.Equal(0m, canceled.LeavesQty);
        Assert.Equal(OrderStatus.Canceled, book.Get("C1")!.Status);
    }

    [Fact]
    public void Cancel_UnknownOrder_ReturnsNull()
    {
        var book = new OrderBook();
        Assert.Null(book.Cancel("unknown"));
    }

    [Fact]
    public void Cancel_AlreadyCanceled_ReturnsNull()
    {
        var book = new OrderBook();
        book.Add("C1", "AAPL", Side.BUY, 100m, 0m, OrdType.MARKET);
        book.Cancel("C1");

        Assert.Null(book.Cancel("C1"));
    }

    [Fact]
    public void Replace_KnownOrder_UpdatesQtyAndClOrdID()
    {
        var book = new OrderBook();
        book.Add("C1", "AAPL", Side.BUY, 100m, 50m, OrdType.LIMIT);

        var replaced = book.Replace("C1", "C2", newQty: 150m, newPrice: 51m);

        Assert.NotNull(replaced);
        Assert.Equal("C2", replaced!.ClOrdID);
        Assert.Equal(150m, replaced.OrderQty);
        Assert.Equal(51m, replaced.Price);
        Assert.Equal(OrderStatus.Replaced, replaced.Status);
        Assert.Null(book.Get("C1"));
        Assert.NotNull(book.Get("C2"));
    }

    [Fact]
    public void Replace_UnknownOrigClOrdID_ReturnsNull()
    {
        var book = new OrderBook();
        Assert.Null(book.Replace("missing", "C2", 100m, 0m));
    }

    [Fact]
    public void Replace_NewClOrdIDAlreadyExists_ReturnsNull()
    {
        var book = new OrderBook();
        book.Add("C1", "AAPL", Side.BUY, 100m, 0m, OrdType.MARKET);
        book.Add("C2", "MSFT", Side.SELL, 50m, 0m, OrdType.MARKET);

        Assert.Null(book.Replace("C1", "C2", 200m, 0m));
        // Original is unchanged.
        Assert.Equal(100m, book.Get("C1")!.OrderQty);
    }

    [Fact]
    public void Replace_CanceledOrder_ReturnsNull()
    {
        var book = new OrderBook();
        book.Add("C1", "AAPL", Side.BUY, 100m, 0m, OrdType.MARKET);
        book.Cancel("C1");

        Assert.Null(book.Replace("C1", "C2", 200m, 0m));
    }
}
