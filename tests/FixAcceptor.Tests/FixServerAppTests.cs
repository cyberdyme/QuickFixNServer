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
}
