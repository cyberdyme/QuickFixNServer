using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace FixAcceptor.Services;

public interface IFixMessageHandler
{
    void HandleNewOrderSingle(QuickFix.FIX44.NewOrderSingle order, SessionID sessionId);
}

public class FixMessageHandler : IFixMessageHandler
{
    private readonly ILogger<FixMessageHandler> _logger;

    public FixMessageHandler(ILogger<FixMessageHandler> logger)
    {
        _logger = logger;
    }

    public void HandleNewOrderSingle(QuickFix.FIX44.NewOrderSingle order, SessionID sessionId)
    {
        var clOrdId = order.ClOrdID.Value;
        var symbol = order.Symbol.Value;
        var side = order.Side.Value;
        var orderQty = order.OrderQty.Value;

        _logger.LogInformation(
            "NewOrderSingle received: ClOrdID={ClOrdId}, Symbol={Symbol}, Side={Side}, Qty={Qty}",
            clOrdId, symbol, side, orderQty);

        var execReport = BuildExecutionReport(clOrdId, symbol, side, orderQty);

        _logger.LogInformation(
            "Sending ExecutionReport: OrderID={OrderId}, ExecID={ExecId}, Symbol={Symbol}",
            execReport.OrderID.Value, execReport.ExecID.Value, symbol);

        Session.SendToTarget(execReport, sessionId);
    }

    public static QuickFix.FIX44.ExecutionReport BuildExecutionReport(
        string clOrdId, string symbol, char side, decimal orderQty)
    {
        var orderId = Guid.NewGuid().ToString("N");
        var execId = Guid.NewGuid().ToString("N");

        var execReport = new QuickFix.FIX44.ExecutionReport(
            new OrderID(orderId),
            new ExecID(execId),
            new ExecType(ExecType.FILL),
            new OrdStatus(OrdStatus.FILLED),
            new Symbol(symbol),
            new Side(side),
            new LeavesQty(0m),
            new CumQty(orderQty),
            new AvgPx(100m));

        execReport.Set(new ClOrdID(clOrdId));
        execReport.Set(new LastQty(orderQty));
        execReport.Set(new LastPx(100m));

        return execReport;
    }
}
