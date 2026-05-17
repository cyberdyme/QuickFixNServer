using QuickFix;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class SessionRegistryTests
{
    [Fact]
    public void Register_AddsSession()
    {
        var registry = new SessionRegistry();
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");

        registry.Register(sessionId);

        var sessions = registry.GetActiveSessions();
        Assert.Single(sessions);
        Assert.Contains(sessionId, sessions);
    }

    [Fact]
    public void Register_SameSessionTwice_DoesNotDuplicate()
    {
        var registry = new SessionRegistry();
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");

        registry.Register(sessionId);
        registry.Register(sessionId);

        Assert.Single(registry.GetActiveSessions());
    }

    [Fact]
    public void Unregister_RemovesSession()
    {
        var registry = new SessionRegistry();
        var sessionId = new SessionID("FIX.4.4", "SERVER", "CLIENT");

        registry.Register(sessionId);
        registry.Unregister(sessionId);

        Assert.False(registry.HasActiveSessions());
        Assert.Empty(registry.GetActiveSessions());
    }

    [Fact]
    public void HasActiveSessions_FalseWhenEmpty()
    {
        var registry = new SessionRegistry();
        Assert.False(registry.HasActiveSessions());
    }

    [Fact]
    public void HasActiveSessions_TrueAfterRegister()
    {
        var registry = new SessionRegistry();
        registry.Register(new SessionID("FIX.4.4", "SERVER", "CLIENT"));

        Assert.True(registry.HasActiveSessions());
    }

    [Fact]
    public void GetActiveSessions_ReturnsMultipleSessions()
    {
        var registry = new SessionRegistry();
        var s1 = new SessionID("FIX.4.4", "SERVER", "CLIENT1");
        var s2 = new SessionID("FIX.4.4", "SERVER", "CLIENT2");

        registry.Register(s1);
        registry.Register(s2);

        var sessions = registry.GetActiveSessions();
        Assert.Equal(2, sessions.Count);
    }
}
