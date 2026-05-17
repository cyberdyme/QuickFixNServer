using QuickFix.Fields;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class ExecutionReportBuilderTests
{
    [Fact]
    public void BuildExecutionReport_SetsRequiredFields()
    {
        var report = FixMessageHandler.BuildExecutionReport(
            clOrdId: "ORD001",
            symbol: "AAPL",
            side: Side.BUY,
            orderQty: 100m);

        Assert.Equal("ORD001", report.ClOrdID.Value);
        Assert.Equal("AAPL", report.Symbol.Value);
        Assert.Equal(Side.BUY, report.Side.Value);
        Assert.Equal(ExecType.FILL, report.ExecType.Value);
        Assert.Equal(OrdStatus.FILLED, report.OrdStatus.Value);
        Assert.Equal(0m, report.LeavesQty.Value);
        Assert.Equal(100m, report.CumQty.Value);
        Assert.Equal(100m, report.AvgPx.Value);
        Assert.Equal(100m, report.LastQty.Value);
        Assert.Equal(100m, report.LastPx.Value);
    }

    [Fact]
    public void BuildExecutionReport_GeneratesUniqueIds()
    {
        var report1 = FixMessageHandler.BuildExecutionReport("A", "MSFT", Side.SELL, 50m);
        var report2 = FixMessageHandler.BuildExecutionReport("B", "MSFT", Side.SELL, 50m);

        Assert.NotEqual(report1.OrderID.Value, report2.OrderID.Value);
        Assert.NotEqual(report1.ExecID.Value, report2.ExecID.Value);
    }

    [Fact]
    public void BuildExecutionReport_OrderIdAndExecIdAreGuidNFormat()
    {
        var report = FixMessageHandler.BuildExecutionReport("X", "GOOG", Side.BUY, 10m);

        // GUID "N" format: 32 hex chars, no dashes
        Assert.Equal(32, report.OrderID.Value.Length);
        Assert.Equal(32, report.ExecID.Value.Length);
        Assert.True(Guid.TryParseExact(report.OrderID.Value, "N", out _));
        Assert.True(Guid.TryParseExact(report.ExecID.Value, "N", out _));
    }

    [Theory]
    [InlineData(Side.BUY)]
    [InlineData(Side.SELL)]
    public void BuildExecutionReport_PreservesSide(char side)
    {
        var report = FixMessageHandler.BuildExecutionReport("C1", "TSLA", side, 25m);
        Assert.Equal(side, report.Side.Value);
    }
}
