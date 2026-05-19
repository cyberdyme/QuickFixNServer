using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace FixAcceptor.Services;

public interface IFixMessageHandler
{
    void HandleNewOrderSingle(QuickFix.FIX44.NewOrderSingle order, SessionID sessionId);
    void HandleOrderCancelRequest(QuickFix.FIX44.OrderCancelRequest request, SessionID sessionId);
    void HandleOrderCancelReplaceRequest(QuickFix.FIX44.OrderCancelReplaceRequest request, SessionID sessionId);
    void HandleOrderStatusRequest(QuickFix.FIX44.OrderStatusRequest request, SessionID sessionId);
}

public class FixMessageHandler : IFixMessageHandler
{
    private readonly IOrderBook _orderBook;
    private readonly IFixMessageSender _sender;
    private readonly ILogger<FixMessageHandler> _logger;

    public FixMessageHandler(
        IOrderBook orderBook,
        IFixMessageSender sender,
        ILogger<FixMessageHandler> logger)
    {
        _orderBook = orderBook;
        _sender = sender;
        _logger = logger;
    }

    public void HandleNewOrderSingle(QuickFix.FIX44.NewOrderSingle order, SessionID sessionId)
    {
        var clOrdId = order.ClOrdID.Value;
        var symbol = order.Symbol.Value;
        var side = order.Side.Value;
        var orderQty = order.OrderQty.Value;
        var ordType = order.OrdType.Value;
        var price = order.IsSetField(Tags.Price) ? order.Price.Value : 0m;

        _logger.LogInformation(
            "NewOrderSingle: ClOrdID={ClOrdId} Symbol={Symbol} Side={Side} Qty={Qty} OrdType={OrdType} Px={Price}",
            clOrdId, symbol, side, orderQty, ordType, price);

        OrderState state;
        try
        {
            state = _orderBook.Add(clOrdId, symbol, side, orderQty, price, ordType);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Duplicate ClOrdID rejected: {ClOrdId}", clOrdId);
            return;
        }

        var ack = ExecutionReportBuilder.NewAck(state);
        _logger.LogInformation(
            "ExecutionReport(New): OrderID={OrderId} ClOrdID={ClOrdId}",
            ack.OrderID.Value, ack.ClOrdID.Value);
        _sender.SendToTarget(ack, sessionId);
    }

    public void HandleOrderCancelRequest(QuickFix.FIX44.OrderCancelRequest request, SessionID sessionId)
    {
        var origClOrdId = request.OrigClOrdID.Value;
        var clOrdId = request.ClOrdID.Value;

        _logger.LogInformation(
            "OrderCancelRequest: ClOrdID={ClOrdId} OrigClOrdID={Orig}",
            clOrdId, origClOrdId);

        var canceled = _orderBook.Cancel(origClOrdId);
        if (canceled is null)
        {
            var reject = OrderCancelRejectBuilder.UnknownOrder(
                clOrdId, origClOrdId, CxlRejResponseTo.ORDER_CANCEL_REQUEST);
            _logger.LogWarning(
                "OrderCancelReject: ClOrdID={ClOrdId} OrigClOrdID={Orig} (unknown / non-cancelable)",
                clOrdId, origClOrdId);
            _sender.SendToTarget(reject, sessionId);
            return;
        }

        var ack = ExecutionReportBuilder.CancelAck(canceled, clOrdId, origClOrdId);
        _logger.LogInformation(
            "ExecutionReport(Canceled): OrderID={OrderId} OrigClOrdID={Orig}",
            ack.OrderID.Value, origClOrdId);
        _sender.SendToTarget(ack, sessionId);
    }

    public void HandleOrderCancelReplaceRequest(QuickFix.FIX44.OrderCancelReplaceRequest request, SessionID sessionId)
    {
        var origClOrdId = request.OrigClOrdID.Value;
        var newClOrdId = request.ClOrdID.Value;
        var newQty = request.OrderQty.Value;
        var newPrice = request.IsSetField(Tags.Price) ? request.Price.Value : 0m;

        _logger.LogInformation(
            "OrderCancelReplaceRequest: ClOrdID={ClOrdId} OrigClOrdID={Orig} Qty={Qty} Px={Px}",
            newClOrdId, origClOrdId, newQty, newPrice);

        var replaced = _orderBook.Replace(origClOrdId, newClOrdId, newQty, newPrice);
        if (replaced is null)
        {
            var reject = OrderCancelRejectBuilder.UnknownOrder(
                newClOrdId, origClOrdId, CxlRejResponseTo.ORDER_CANCEL_REPLACE_REQUEST);
            _logger.LogWarning(
                "OrderCancelReject (Replace): ClOrdID={ClOrdId} OrigClOrdID={Orig} (unknown / non-cancelable / duplicate)",
                newClOrdId, origClOrdId);
            _sender.SendToTarget(reject, sessionId);
            return;
        }

        var ack = ExecutionReportBuilder.ReplaceAck(replaced, origClOrdId);
        _logger.LogInformation(
            "ExecutionReport(Replaced): OrderID={OrderId} ClOrdID={ClOrdId} OrigClOrdID={Orig}",
            ack.OrderID.Value, newClOrdId, origClOrdId);
        _sender.SendToTarget(ack, sessionId);
    }

    public void HandleOrderStatusRequest(QuickFix.FIX44.OrderStatusRequest request, SessionID sessionId)
    {
        var clOrdId = request.ClOrdID.Value;
        _logger.LogInformation("OrderStatusRequest: ClOrdID={ClOrdId}", clOrdId);

        var order = _orderBook.Get(clOrdId);
        if (order is null)
        {
            // No matching order — reply with a stub OrderStatus saying Rejected,
            // which is a common acceptor behavior for an unknown ClOrdID.
            var stub = new OrderState(
                ClOrdID: clOrdId,
                OrderID: "NONE",
                Symbol: request.Symbol.Value,
                Side: request.Side.Value,
                OrderQty: 0m,
                Price: 0m,
                OrdType: '?',
                Status: OrderStatus.Rejected,
                CumQty: 0m,
                LeavesQty: 0m);
            var unknown = ExecutionReportBuilder.StatusSnapshot(stub);
            _logger.LogWarning("OrderStatus snapshot for unknown ClOrdID={ClOrdId}", clOrdId);
            _sender.SendToTarget(unknown, sessionId);
            return;
        }

        var snapshot = ExecutionReportBuilder.StatusSnapshot(order);
        _sender.SendToTarget(snapshot, sessionId);
    }
}
