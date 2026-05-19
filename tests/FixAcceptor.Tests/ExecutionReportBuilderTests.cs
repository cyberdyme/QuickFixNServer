using QuickFix.Fields;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class ExecutionReportBuilderTests
{
    private static OrderState SampleOrder(
        string clOrdId = "ORD001",
        string symbol = "AAPL",
        char side = Side.BUY,
        decimal qty = 100m,
        decimal price = 0m,
        OrderStatus status = OrderStatus.New,
        decimal cumQty = 0m,
        decimal? leavesQty = null) =>
        new(
            ClOrdID: clOrdId,
            OrderID: "ORDID-" + clOrdId,
            Symbol: symbol,
            Side: side,
            OrderQty: qty,
            Price: price,
            OrdType: OrdType.LIMIT,
            Status: status,
            CumQty: cumQty,
            LeavesQty: leavesQty ?? (qty - cumQty));

    [Fact]
    public void NewAck_HasExecTypeAndOrdStatusNew()
    {
        var report = ExecutionReportBuilder.NewAck(SampleOrder());

        Assert.Equal(ExecType.NEW, report.ExecType.Value);
        Assert.Equal(OrdStatus.NEW, report.OrdStatus.Value);
        Assert.Equal("ORD001", report.ClOrdID.Value);
        Assert.Equal("AAPL", report.Symbol.Value);
        Assert.Equal(100m, report.LeavesQty.Value);
        Assert.Equal(0m, report.CumQty.Value);
    }

    [Fact]
    public void NewAck_AllocatesUniqueExecID()
    {
        var r1 = ExecutionReportBuilder.NewAck(SampleOrder("A"));
        var r2 = ExecutionReportBuilder.NewAck(SampleOrder("B"));

        Assert.NotEqual(r1.ExecID.Value, r2.ExecID.Value);
        Assert.True(Guid.TryParseExact(r1.ExecID.Value, "N", out _));
    }

    [Fact]
    public void Fill_FullSize_SetsOrdStatusFilled()
    {
        var report = ExecutionReportBuilder.Fill(SampleOrder(qty: 100m), lastQty: 100m, lastPx: 50m);

        Assert.Equal(ExecType.TRADE, report.ExecType.Value);
        Assert.Equal(OrdStatus.FILLED, report.OrdStatus.Value);
        Assert.Equal(0m, report.LeavesQty.Value);
        Assert.Equal(100m, report.CumQty.Value);
        Assert.Equal(50m, report.LastPx.Value);
        Assert.Equal(100m, report.LastQty.Value);
    }

    [Fact]
    public void Fill_PartialSize_SetsOrdStatusPartiallyFilled()
    {
        var report = ExecutionReportBuilder.Fill(SampleOrder(qty: 100m), lastQty: 40m, lastPx: 50m);

        Assert.Equal(ExecType.TRADE, report.ExecType.Value);
        Assert.Equal(OrdStatus.PARTIALLY_FILLED, report.OrdStatus.Value);
        Assert.Equal(60m, report.LeavesQty.Value);
        Assert.Equal(40m, report.CumQty.Value);
    }

    [Fact]
    public void CancelAck_SetsExecTypeAndOrigClOrdID()
    {
        var canceled = SampleOrder(status: OrderStatus.Canceled, leavesQty: 0m);
        var report = ExecutionReportBuilder.CancelAck(canceled, cancelClOrdId: "C1", origClOrdId: "ORD001");

        Assert.Equal(ExecType.CANCELED, report.ExecType.Value);
        Assert.Equal(OrdStatus.CANCELED, report.OrdStatus.Value);
        Assert.Equal("C1", report.ClOrdID.Value);
        Assert.Equal("ORD001", report.OrigClOrdID.Value);
        Assert.Equal(0m, report.LeavesQty.Value);
    }

    [Fact]
    public void ReplaceAck_NoFills_KeepsOrdStatusNew()
    {
        var replaced = SampleOrder(clOrdId: "ORD001b", qty: 150m, status: OrderStatus.Replaced, leavesQty: 150m);
        var report = ExecutionReportBuilder.ReplaceAck(replaced, origClOrdId: "ORD001");

        Assert.Equal(ExecType.REPLACE, report.ExecType.Value);
        Assert.Equal(OrdStatus.NEW, report.OrdStatus.Value);
        Assert.Equal("ORD001b", report.ClOrdID.Value);
        Assert.Equal("ORD001", report.OrigClOrdID.Value);
        Assert.Equal(150m, report.LeavesQty.Value);
    }

    [Fact]
    public void StatusSnapshot_MapsOrderStatusToOrdStatus()
    {
        var order = SampleOrder(status: OrderStatus.PartiallyFilled, cumQty: 25m, leavesQty: 75m);
        var report = ExecutionReportBuilder.StatusSnapshot(order);

        Assert.Equal(ExecType.ORDER_STATUS, report.ExecType.Value);
        Assert.Equal(OrdStatus.PARTIALLY_FILLED, report.OrdStatus.Value);
        Assert.Equal(25m, report.CumQty.Value);
        Assert.Equal(75m, report.LeavesQty.Value);
    }

    [Theory]
    [InlineData(Side.BUY)]
    [InlineData(Side.SELL)]
    public void NewAck_PreservesSide(char side)
    {
        var report = ExecutionReportBuilder.NewAck(SampleOrder(side: side));
        Assert.Equal(side, report.Side.Value);
    }
}
