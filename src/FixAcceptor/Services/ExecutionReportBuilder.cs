using QuickFix.Fields;

namespace FixAcceptor.Services;

public static class ExecutionReportBuilder
{
    /// <summary>
    /// Builds an "order accepted" ExecutionReport (ExecType=0 New, OrdStatus=0 New).
    /// Sent in response to a NewOrderSingle that passed validation.
    /// </summary>
    public static QuickFix.FIX44.ExecutionReport NewAck(OrderState order)
    {
        var report = new QuickFix.FIX44.ExecutionReport(
            new OrderID(order.OrderID),
            new ExecID(Guid.NewGuid().ToString("N")),
            new ExecType(ExecType.NEW),
            new OrdStatus(OrdStatus.NEW),
            new Symbol(order.Symbol),
            new Side(order.Side),
            new LeavesQty(order.LeavesQty),
            new CumQty(order.CumQty),
            new AvgPx(0m));
        report.Set(new ClOrdID(order.ClOrdID));
        report.Set(new OrderQty(order.OrderQty));
        return report;
    }

    /// <summary>
    /// Builds a "fully filled" ExecutionReport. The sample server doesn't auto-fill
    /// orders, so this builder is used by the file-replay path and by tests.
    /// </summary>
    public static QuickFix.FIX44.ExecutionReport Fill(OrderState order, decimal lastQty, decimal lastPx)
    {
        var cumQty = order.CumQty + lastQty;
        var leavesQty = Math.Max(0m, order.OrderQty - cumQty);
        var ordStatus = leavesQty == 0m ? OrdStatus.FILLED : OrdStatus.PARTIALLY_FILLED;

        var report = new QuickFix.FIX44.ExecutionReport(
            new OrderID(order.OrderID),
            new ExecID(Guid.NewGuid().ToString("N")),
            new ExecType(ExecType.TRADE),
            new OrdStatus(ordStatus),
            new Symbol(order.Symbol),
            new Side(order.Side),
            new LeavesQty(leavesQty),
            new CumQty(cumQty),
            new AvgPx(lastPx));
        report.Set(new ClOrdID(order.ClOrdID));
        report.Set(new OrderQty(order.OrderQty));
        report.Set(new LastQty(lastQty));
        report.Set(new LastPx(lastPx));
        return report;
    }

    /// <summary>
    /// Builds a "cancel accepted" ExecutionReport (ExecType=4 Canceled, OrdStatus=4 Canceled).
    /// </summary>
    public static QuickFix.FIX44.ExecutionReport CancelAck(OrderState canceled, string cancelClOrdId, string origClOrdId)
    {
        var report = new QuickFix.FIX44.ExecutionReport(
            new OrderID(canceled.OrderID),
            new ExecID(Guid.NewGuid().ToString("N")),
            new ExecType(ExecType.CANCELED),
            new OrdStatus(OrdStatus.CANCELED),
            new Symbol(canceled.Symbol),
            new Side(canceled.Side),
            new LeavesQty(0m),
            new CumQty(canceled.CumQty),
            new AvgPx(0m));
        report.Set(new ClOrdID(cancelClOrdId));
        report.Set(new OrigClOrdID(origClOrdId));
        report.Set(new OrderQty(canceled.OrderQty));
        return report;
    }

    /// <summary>
    /// Builds a "replace accepted" ExecutionReport (ExecType=5 Replaced).
    /// OrdStatus reflects the order's state after the replace.
    /// </summary>
    public static QuickFix.FIX44.ExecutionReport ReplaceAck(OrderState replaced, string origClOrdId)
    {
        var ordStatus = replaced.CumQty == 0m
            ? OrdStatus.NEW
            : (replaced.LeavesQty == 0m ? OrdStatus.FILLED : OrdStatus.PARTIALLY_FILLED);

        var report = new QuickFix.FIX44.ExecutionReport(
            new OrderID(replaced.OrderID),
            new ExecID(Guid.NewGuid().ToString("N")),
            new ExecType(ExecType.REPLACE),
            new OrdStatus(ordStatus),
            new Symbol(replaced.Symbol),
            new Side(replaced.Side),
            new LeavesQty(replaced.LeavesQty),
            new CumQty(replaced.CumQty),
            new AvgPx(0m));
        report.Set(new ClOrdID(replaced.ClOrdID));
        report.Set(new OrigClOrdID(origClOrdId));
        report.Set(new OrderQty(replaced.OrderQty));
        return report;
    }

    /// <summary>
    /// Builds an OrderStatus snapshot (ExecType=I) in response to an OrderStatusRequest.
    /// </summary>
    public static QuickFix.FIX44.ExecutionReport StatusSnapshot(OrderState order)
    {
        var ordStatus = order.Status switch
        {
            OrderStatus.New => OrdStatus.NEW,
            OrderStatus.PartiallyFilled => OrdStatus.PARTIALLY_FILLED,
            OrderStatus.Filled => OrdStatus.FILLED,
            OrderStatus.Canceled => OrdStatus.CANCELED,
            OrderStatus.Replaced => OrdStatus.REPLACED,
            OrderStatus.Rejected => OrdStatus.REJECTED,
            _ => OrdStatus.NEW
        };

        var report = new QuickFix.FIX44.ExecutionReport(
            new OrderID(order.OrderID),
            new ExecID(Guid.NewGuid().ToString("N")),
            new ExecType(ExecType.ORDER_STATUS),
            new OrdStatus(ordStatus),
            new Symbol(order.Symbol),
            new Side(order.Side),
            new LeavesQty(order.LeavesQty),
            new CumQty(order.CumQty),
            new AvgPx(0m));
        report.Set(new ClOrdID(order.ClOrdID));
        report.Set(new OrderQty(order.OrderQty));
        return report;
    }
}
