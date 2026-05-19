using Microsoft.Extensions.Logging.Abstractions;
using QuickFix;
using QuickFix.Fields;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class FixMessageHandlerTests
{
    private readonly OrderBook _book;
    private readonly RecordingSender _sender;
    private readonly FixMessageHandler _handler;
    private readonly SessionID _sessionId = new("FIX.4.4", "SERVER", "CLIENT");

    public FixMessageHandlerTests()
    {
        _book = new OrderBook();
        _sender = new RecordingSender();
        _handler = new FixMessageHandler(_book, _sender, NullLogger<FixMessageHandler>.Instance);
    }

    [Fact]
    public void HandleNewOrderSingle_AcksAndAddsToBook()
    {
        var nos = new QuickFix.FIX44.NewOrderSingle(
            new ClOrdID("C1"),
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT));
        nos.Set(new OrderQty(100m));
        nos.Set(new Price(50m));

        _handler.HandleNewOrderSingle(nos, _sessionId);

        Assert.Single(_sender.Sent);
        var report = Assert.IsType<QuickFix.FIX44.ExecutionReport>(_sender.Sent[0].message);
        Assert.Equal(ExecType.NEW, report.ExecType.Value);
        Assert.Equal("C1", report.ClOrdID.Value);
        Assert.NotNull(_book.Get("C1"));
    }

    [Fact]
    public void HandleOrderCancelRequest_UnknownOrder_SendsCancelReject()
    {
        var cancel = new QuickFix.FIX44.OrderCancelRequest(
            new OrigClOrdID("missing"),
            new ClOrdID("X1"),
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow));

        _handler.HandleOrderCancelRequest(cancel, _sessionId);

        var reject = Assert.IsType<QuickFix.FIX44.OrderCancelReject>(_sender.Sent[0].message);
        Assert.Equal("X1", reject.ClOrdID.Value);
        Assert.Equal("missing", reject.OrigClOrdID.Value);
        Assert.Equal(CxlRejReason.UNKNOWN_ORDER, reject.CxlRejReason.Value);
        Assert.Equal(CxlRejResponseTo.ORDER_CANCEL_REQUEST, reject.CxlRejResponseTo.Value);
    }

    [Fact]
    public void HandleOrderCancelRequest_KnownOrder_SendsCancelAck()
    {
        _book.Add("C1", "AAPL", Side.BUY, 100m, 0m, OrdType.MARKET);
        var cancel = new QuickFix.FIX44.OrderCancelRequest(
            new OrigClOrdID("C1"),
            new ClOrdID("X1"),
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow));

        _handler.HandleOrderCancelRequest(cancel, _sessionId);

        var ack = Assert.IsType<QuickFix.FIX44.ExecutionReport>(_sender.Sent[0].message);
        Assert.Equal(ExecType.CANCELED, ack.ExecType.Value);
        Assert.Equal(OrdStatus.CANCELED, ack.OrdStatus.Value);
        Assert.Equal(OrderStatus.Canceled, _book.Get("C1")!.Status);
    }

    [Fact]
    public void HandleOrderCancelReplaceRequest_KnownOrder_SendsReplaceAck()
    {
        _book.Add("C1", "AAPL", Side.BUY, 100m, 50m, OrdType.LIMIT);
        var replace = new QuickFix.FIX44.OrderCancelReplaceRequest(
            new OrigClOrdID("C1"),
            new ClOrdID("C2"),
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT));
        replace.Set(new OrderQty(150m));
        replace.Set(new Price(51m));

        _handler.HandleOrderCancelReplaceRequest(replace, _sessionId);

        var ack = Assert.IsType<QuickFix.FIX44.ExecutionReport>(_sender.Sent[0].message);
        Assert.Equal(ExecType.REPLACE, ack.ExecType.Value);
        Assert.Equal("C2", ack.ClOrdID.Value);
        Assert.Equal("C1", ack.OrigClOrdID.Value);
        Assert.NotNull(_book.Get("C2"));
        Assert.Null(_book.Get("C1"));
    }

    [Fact]
    public void HandleOrderCancelReplaceRequest_UnknownOrder_SendsCancelReject()
    {
        var replace = new QuickFix.FIX44.OrderCancelReplaceRequest(
            new OrigClOrdID("missing"),
            new ClOrdID("C2"),
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT));
        replace.Set(new OrderQty(150m));

        _handler.HandleOrderCancelReplaceRequest(replace, _sessionId);

        var reject = Assert.IsType<QuickFix.FIX44.OrderCancelReject>(_sender.Sent[0].message);
        Assert.Equal(CxlRejResponseTo.ORDER_CANCEL_REPLACE_REQUEST, reject.CxlRejResponseTo.Value);
    }

    [Fact]
    public void HandleOrderStatusRequest_KnownOrder_SendsStatusSnapshot()
    {
        _book.Add("C1", "AAPL", Side.BUY, 100m, 0m, OrdType.MARKET);
        var status = new QuickFix.FIX44.OrderStatusRequest(
            new ClOrdID("C1"),
            new Symbol("AAPL"),
            new Side(Side.BUY));

        _handler.HandleOrderStatusRequest(status, _sessionId);

        var snap = Assert.IsType<QuickFix.FIX44.ExecutionReport>(_sender.Sent[0].message);
        Assert.Equal(ExecType.ORDER_STATUS, snap.ExecType.Value);
        Assert.Equal(OrdStatus.NEW, snap.OrdStatus.Value);
        Assert.Equal("C1", snap.ClOrdID.Value);
    }

    [Fact]
    public void HandleOrderStatusRequest_UnknownOrder_SendsRejectedStatusSnapshot()
    {
        var status = new QuickFix.FIX44.OrderStatusRequest(
            new ClOrdID("missing"),
            new Symbol("AAPL"),
            new Side(Side.BUY));

        _handler.HandleOrderStatusRequest(status, _sessionId);

        var snap = Assert.IsType<QuickFix.FIX44.ExecutionReport>(_sender.Sent[0].message);
        Assert.Equal(ExecType.ORDER_STATUS, snap.ExecType.Value);
        Assert.Equal(OrdStatus.REJECTED, snap.OrdStatus.Value);
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
