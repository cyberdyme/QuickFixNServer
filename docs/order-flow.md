# Order flow

`FixMessageHandler` is the application-layer logic for the four FIX 4.4
order-management messages. State lives in `OrderBook` (in-memory; resets on
process restart).

## NewOrderSingle (35=D)

```
Client                                      Server
  |                                           |
  | NewOrderSingle  ClOrdID=C1                |
  | Symbol=AAPL Side=BUY Qty=100 OrdType=2    |
  | -----------------------------------► OnMessage(NewOrderSingle)
  |                                           ├─ OrderBook.Add(C1, AAPL, BUY, 100)
  |                                           │       → OrderState{Status=New, LeavesQty=100}
  |                                           ├─ ExecutionReportBuilder.NewAck(state)
  |                                           │       150=0 (New), 39=0 (New)
  |                                           ▼
  | ◄────────────────────────────────── ExecutionReport (New)
```

The acceptor does **not** auto-fill orders. Sending a `NewOrderSingle` leaves
the order in state `New` so it can be cancelled, replaced, or filled via the
file-replay path (see `ExecutionFileWatcherService`).

Duplicate `ClOrdID` is rejected by `OrderBook.Add` (throws
`InvalidOperationException`) and the handler logs a warning without
emitting an ExecutionReport.

## OrderCancelRequest (35=F)

```
  | OrderCancelRequest ClOrdID=X1 OrigClOrdID=C1
  | -----------------------------------► OnMessage(OrderCancelRequest)
  |                                           ├─ OrderBook.Cancel(C1)
  |                                           │     ├ found + cancelable
  |                                           │     │  → mark Canceled, LeavesQty=0
  |                                           │     │  → ExecutionReport(Canceled)
  |                                           │     │      150=4, 39=4
  |                                           │     └ unknown / terminal
  |                                           │       → OrderCancelReject(35=9)
  |                                           │           CxlRejReason=1 (UNKNOWN_ORDER)
  |                                           │           CxlRejResponseTo=1
  |                                           ▼
  | ◄────────────────────────────────── ExecutionReport(Canceled)  OR
  | ◄────────────────────────────────── OrderCancelReject
```

## OrderCancelReplaceRequest (35=G)

```
  | OrderCancelReplaceRequest
  |   ClOrdID=C2 OrigClOrdID=C1
  |   OrderQty=150 Price=51
  | -----------------------------------► OnMessage(OrderCancelReplaceRequest)
  |                                           ├─ OrderBook.Replace(C1, C2, 150, 51)
  |                                           │     - removes index by C1
  |                                           │     - indexes the order under C2
  |                                           │     - Status=Replaced
  |                                           ├─ ExecutionReportBuilder.ReplaceAck
  |                                           │     150=5 (Replace)
  |                                           │     ClOrdID=C2 OrigClOrdID=C1
  |                                           ▼
  | ◄────────────────────────────────── ExecutionReport(Replaced)  OR
  | ◄────────────────────────────────── OrderCancelReject (CxlRejResponseTo=2)
```

Replace fails (and produces an `OrderCancelReject`) when:
- The original `ClOrdID` is unknown.
- The original order is no longer cancelable (already Canceled, Filled,
  Replaced, Rejected).
- The new `ClOrdID` already exists in the book.

## OrderStatusRequest (35=H)

```
  | OrderStatusRequest ClOrdID=C1 Symbol=AAPL Side=BUY
  | -----------------------------------► OnMessage(OrderStatusRequest)
  |                                           ├─ OrderBook.Get(C1)
  |                                           │     ├ found
  |                                           │     │  → ExecutionReportBuilder.StatusSnapshot
  |                                           │     │      150=I (OrderStatus)
  |                                           │     │      39 = current state (New / PartiallyFilled / …)
  |                                           │     └ not found
  |                                           │       → ExecutionReport(I) with 39=8 (Rejected)
  |                                           ▼
  | ◄────────────────────────────────── ExecutionReport(OrderStatus)
```

## State machine

```
   New ──► PartiallyFilled ──► Filled
    │            │
    │            ▼
    └──► Canceled / Replaced
                 │
                 ▼
              Rejected (terminal)
```

`OrderBook.IsCancelable` allows cancel and replace only from `New` and
`PartiallyFilled`. Anything else returns `null` and the handler emits a
reject.

## File-replay path

`ExecutionFileWatcherService` watches `executions/` for files. Each non-empty
line is parsed as a raw FIX message (SOH or `|` delimited) and sent to every
active session via `Session.SendToTarget`. This is how you drive synthetic
fills against orders sitting in the book — drop a file with a 35=8
ExecutionReport with `ExecType=F (Trade)` for the relevant `ClOrdID` and the
client sees the fill.
