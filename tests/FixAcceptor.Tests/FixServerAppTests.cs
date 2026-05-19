using Microsoft.Extensions.Logging;
using Moq;
using QuickFix;
using QuickFix.Fields;
using FixAcceptor.Fix;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class FixServerAppTests
{
    private readonly Mock<IFixMessageHandler> _handlerMock;
    private readonly Mock<ISessionRegistry> _sessionRegistryMock;
    private readonly FixServerApp _app;

    public FixServerAppTests()
    {
        _handlerMock = new Mock<IFixMessageHandler>();
        _sessionRegistryMock = new Mock<ISessionRegistry>();
        var loggerMock = new Mock<ILogger<FixServerApp>>();
        _app = new FixServerApp(
            _handlerMock.Object,
            _sessionRegistryMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public void FromApp_WithNewOrderSingle_DelegatesToHandler()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var nos = new QuickFix.FIX44.NewOrderSingle(
            new ClOrdID("TEST001"),
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.MARKET));
        nos.Set(new OrderQty(100m));

        _app.FromApp(nos, sessionId);

        _handlerMock.Verify(
            h => h.HandleNewOrderSingle(
                It.Is<QuickFix.FIX44.NewOrderSingle>(o => o.ClOrdID.Value == "TEST001"),
                sessionId),
            Times.Once);
    }

    [Fact]
    public void OnCreate_DoesNotThrow()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var ex = Record.Exception(() => _app.OnCreate(sessionId));
        Assert.Null(ex);
    }

    [Fact]
    public void OnLogon_RegistersSession()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");

        _app.OnLogon(sessionId);

        _sessionRegistryMock.Verify(r => r.Register(sessionId), Times.Once);
    }

    [Fact]
    public void OnLogout_UnregistersSession()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");

        _app.OnLogout(sessionId);

        _sessionRegistryMock.Verify(r => r.Unregister(sessionId), Times.Once);
    }

    [Theory]
    [InlineData("A")] // Logon
    [InlineData("5")] // Logout
    [InlineData("0")] // Heartbeat
    [InlineData("1")] // TestRequest
    [InlineData("2")] // ResendRequest
    [InlineData("3")] // Reject
    [InlineData("4")] // SequenceReset
    [InlineData("Z")] // unknown admin type — exercises default branch
    public void FromAdmin_HandlesKnownAdminMessageTypes(string msgType)
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var msg = new Message();
        msg.Header.SetField(new MsgType(msgType));

        var ex = Record.Exception(() => _app.FromAdmin(msg, sessionId));

        Assert.Null(ex);
    }

    [Fact]
    public void FromApp_WithOrderCancelRequest_DelegatesToHandler()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var cancel = new QuickFix.FIX44.OrderCancelRequest(
            new OrigClOrdID("ORIG"),
            new ClOrdID("CXL1"),
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow));

        _app.FromApp(cancel, sessionId);

        _handlerMock.Verify(
            h => h.HandleOrderCancelRequest(
                It.Is<QuickFix.FIX44.OrderCancelRequest>(o => o.ClOrdID.Value == "CXL1"),
                sessionId),
            Times.Once);
    }

    [Fact]
    public void FromApp_WithOrderCancelReplaceRequest_DelegatesToHandler()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var replace = new QuickFix.FIX44.OrderCancelReplaceRequest(
            new OrigClOrdID("ORIG"),
            new ClOrdID("REPL1"),
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT));
        replace.Set(new OrderQty(50m));

        _app.FromApp(replace, sessionId);

        _handlerMock.Verify(
            h => h.HandleOrderCancelReplaceRequest(
                It.Is<QuickFix.FIX44.OrderCancelReplaceRequest>(o => o.ClOrdID.Value == "REPL1"),
                sessionId),
            Times.Once);
    }

    [Fact]
    public void FromApp_WithOrderStatusRequest_DelegatesToHandler()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var status = new QuickFix.FIX44.OrderStatusRequest(
            new ClOrdID("STATUS1"),
            new Symbol("AAPL"),
            new Side(Side.BUY));

        _app.FromApp(status, sessionId);

        _handlerMock.Verify(
            h => h.HandleOrderStatusRequest(
                It.Is<QuickFix.FIX44.OrderStatusRequest>(o => o.ClOrdID.Value == "STATUS1"),
                sessionId),
            Times.Once);
    }

    [Fact]
    public void FromApp_WithUnsupportedMessageType_RethrowsSoEngineCanReject()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var unsupported = new Message();
        // 'BE' = UserRequest in FIX 4.4 — we have no OnMessage handler for it,
        // so MessageCracker.Crack throws UnsupportedMessageType, which we must
        // re-raise so QuickFIX/n emits a BusinessMessageReject (35=j).
        unsupported.Header.SetField(new MsgType("BE"));

        Assert.Throws<UnsupportedMessageType>(() => _app.FromApp(unsupported, sessionId));
        _handlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ToAdmin_DoesNotThrowOnWellFormedMessage()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var msg = new Message();
        msg.Header.SetField(new MsgType("A"));

        var ex = Record.Exception(() => _app.ToAdmin(msg, sessionId));

        Assert.Null(ex);
    }

    [Fact]
    public void ToApp_DoesNotThrowOnWellFormedMessage()
    {
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");
        var msg = new Message();
        msg.Header.SetField(new MsgType("D"));

        var ex = Record.Exception(() => _app.ToApp(msg, sessionId));

        Assert.Null(ex);
    }
}
