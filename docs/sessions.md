# Session layer

The sample acceptor delegates protocol mechanics to QuickFIX/n and adds
structured logging around each session callback. This page maps the FIX
session state machine to the callbacks in `FixServerApp` so you can see what
the engine handles vs. what the application observes.

## Lifecycle

```
                        +-------------------+
TCP accept ───────────► |     OnCreate      |   session row materialized
                        +---------+---------+
                                  │
                                  ▼
            inbound Logon ──► FromAdmin("A")  ──► QuickFIX/n validates
                                  │                (HeartBtInt, EncryptMethod,
                                  ▼                 ResetSeqNumFlag)
                              ToAdmin("A")  ──► outbound Logon
                                  │
                                  ▼
                              OnLogon  ◄──────  session is "logged on"
                                  │              ─► SessionRegistry.Register
                                  ▼
            ┌─── Heartbeat / TestRequest / ResendRequest / SequenceReset
            │    are exchanged automatically by QuickFIX/n; we log each
            │    in FromAdmin without intervening.
            │
            ▼
       app traffic (FromApp / ToApp)
            │
            ▼
            Logout (initiated by either side)
                                  │
                                  ▼
                              FromAdmin("5")  ──► logged
                              ToAdmin("5")    ──► outbound Logout
                              OnLogout        ──► SessionRegistry.Unregister
                                                  + MarketDataSubscriptions cleaned
```

## What QuickFIX/n owns vs. what we own

QuickFIX/n handles all of the wire-level protocol invariants. We do not have
to write code for any of these — but the engine surfaces each one through a
callback so the application can observe or modify what is happening.

| Concern                              | Owned by QuickFIX/n | Callback we use      |
|--------------------------------------|---------------------|----------------------|
| MsgSeqNum tracking                   | yes                 | —                    |
| BodyLength / CheckSum                | yes                 | —                    |
| Heartbeat scheduling                 | yes                 | `FromAdmin("0")` log |
| TestRequest → Heartbeat reply        | yes                 | `FromAdmin("1")` log |
| ResendRequest → replay from store    | yes                 | `FromAdmin("2")` log |
| SequenceReset / GapFill              | yes                 | `FromAdmin("4")` log |
| Inbound Logon validation             | yes                 | `FromAdmin("A")` log |
| Outbound Logon construction          | yes                 | `ToAdmin("A")` log   |
| Logout handshake                     | yes                 | `FromAdmin("5")` + `OnLogout` |
| Session-level Reject (35=3)          | yes (on receive)    | `FromAdmin("3")` log |
| BusinessMessageReject for unknown app msg | yes              | `FromApp` re-raises `UnsupportedMessageType` |
| Tracking active sessions             | no                  | `SessionRegistry`    |
| Cleaning up market-data subscriptions | no                  | `OnLogout`           |

## server.cfg knobs that shape the session

`src/FixAcceptor/server.cfg` ships every standard session-policy field set
explicitly so the defaults are visible:

- `ResetOnLogon=Y` — friendly default for a sample; flip to `N` for a
  production acceptor where sequence numbers must persist across drops.
- `PersistMessages=Y` — outbound app messages are stored so we can answer
  `ResendRequest` from disk.
- `LogonTimeout=10` / `LogoutTimeout=2` — handshake bounds.
- `CheckLatency=Y` + `MaxLatency=120` — reject inbound messages whose
  `SendingTime` is more than 2 minutes from our clock.
- `SocketReuseAddress=Y` — allows quick restarts to re-bind the same port.

## Unknown application messages

If a client sends an application message the cracker has no handler for, the
flow is:

1. `FromApp` calls `Crack(message, sessionId)`.
2. `MessageCracker` throws `QuickFix.UnsupportedMessageType`.
3. We catch it, emit a warning log line with the offending `MsgType`, then
   re-throw.
4. QuickFIX/n's session loop responds with `BusinessMessageReject` (35=j).

This is why the test
`FromApp_WithUnsupportedMessageType_RethrowsSoEngineCanReject` asserts that
the exception propagates rather than being swallowed.
