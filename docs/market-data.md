# Market data + reference data

`MarketDataHandler` answers the three reference / market-data messages a
client typically asks an acceptor at startup:

- `MarketDataRequest` (35=V)
- `SecurityListRequest` (35=x)
- `TradingSessionStatusRequest` (35=g)

The instrument universe is a static table in `SecurityUniverse`:

| Symbol  | Seed mid-price |
|---------|----------------|
| AAPL    | 175            |
| MSFT    | 350            |
| GOOG    | 140            |
| TSLA    | 220            |
| TEST.A  | 100            |

Mid prices walk on a small random walk during a subscription (capped at
±0.10 per tick) so the demo prices drift but do not run away.

## SecurityListRequest (35=x → 35=y)

```
Client                              Server
  |                                   |
  | SecurityListRequest               |
  |  SecurityReqID=LIST1              |
  |  SecurityListRequestType=0        |
  | ────────────────────────────► HandleSecurityListRequest
  |                                   ├─ universe.AllSymbols()
  |                                   ▼
  | ◄──────────────────────── SecurityList (35=y)
  |    SecurityRequestResult=0 (Valid)
  |    NoRelatedSym = 5 entries (AAPL, MSFT, GOOG, TSLA, TEST.A)
```

## TradingSessionStatusRequest (35=g → 35=h)

```
  | TradingSessionStatusRequest TradSesReqID=TSS1
  | ────────────────────────────► HandleTradingSessionStatusRequest
  | ◄──────────────────────── TradingSessionStatus
  |    TradingSessionID=DAY
  |    TradSesStatus=2 (Open)
```

The sample always reports the venue as Open; a real acceptor would gate this
on `StartTime` / `EndTime` from `server.cfg`.

## MarketDataRequest (35=V)

The `SubscriptionRequestType(263)` byte decides the response:

```
SubType=0 (Snapshot)
  | MarketDataRequest MDReqID=R1
  |  SubType=0 NoRelatedSym=[AAPL]
  | ────────────────────────────► HandleMarketDataRequest
  |                                   ├─ all symbols known?
  |                                   │     no → MarketDataRequestReject(35=Y)
  |                                   │            MDReqRejReason=0 (Unknown symbol)
  |                                   │     yes →
  |                                   ▼
  | ◄──────────────────────── MarketDataSnapshotFullRefresh(35=W)
  |    one message per symbol
  |    bid = mid − 0.05, offer = mid + 0.05
  |    bid/offer size = 1000
  |
  | (subscription NOT recorded; no further updates)


SubType=1 (Snapshot + Updates)
  | MarketDataRequest MDReqID=R2 SubType=1 …
  | ────────────────────────────► HandleMarketDataRequest
  |                                   ├─ symbol check
  |                                   ├─ MarketDataSubscriptions.Add(R2, sessionId, [AAPL])
  |                                   ▼
  | ◄──────────────────────── MarketDataSnapshotFullRefresh (initial)
  |
  | (every TickIntervalSeconds — default 1s)
  | ◄──────────────────────── MarketDataIncrementalRefresh(35=X)
  |    per symbol in the subscription, with a random-walked mid


SubType=2 (Unsubscribe)
  | MarketDataRequest MDReqID=R2 SubType=2
  | ────────────────────────────► MarketDataSubscriptions.Remove(R2)
  |                                   (no reply)


SubType=anything else
  | ────────────────────────────► MarketDataRequestReject(35=Y)
  |                                   MDReqRejReason=4 (Unsupported SubscriptionRequestType)
```

## Subscription cleanup

`FixServerApp.OnLogout` calls
`MarketDataSubscriptions.RemoveAllForSession(sessionId)` so a client that
drops without sending `SubType=2` does not keep the publisher ticking
against a dead socket.

## Tuning the publisher

`appsettings.json` exposes `FixAcceptor.MarketDataTickIntervalSeconds`
(default `1.0`). Lower it for a chatty demo, raise it if you want quieter
logs.
