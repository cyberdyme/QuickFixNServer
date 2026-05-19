using QuickFix.Fields;

namespace FixAcceptor.Services;

public static class OrderCancelRejectBuilder
{
    /// <summary>
    /// Builds an OrderCancelReject (35=9) for a cancel/replace that referenced
    /// an unknown or already-terminal ClOrdID. Use <c>CxlRejResponseTo</c>:
    /// '1' for a rejected OrderCancelRequest, '2' for a rejected
    /// OrderCancelReplaceRequest.
    /// </summary>
    public static QuickFix.FIX44.OrderCancelReject UnknownOrder(
        string clOrdId,
        string origClOrdId,
        char cxlRejResponseTo,
        string text = "Unknown or non-cancelable order")
    {
        var reject = new QuickFix.FIX44.OrderCancelReject(
            new OrderID("NONE"),
            new ClOrdID(clOrdId),
            new OrigClOrdID(origClOrdId),
            new OrdStatus(OrdStatus.REJECTED),
            new CxlRejResponseTo(cxlRejResponseTo));
        reject.Set(new CxlRejReason(CxlRejReason.UNKNOWN_ORDER));
        reject.Set(new Text(text));
        return reject;
    }
}
