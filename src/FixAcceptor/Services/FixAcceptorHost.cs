using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Store;
using FixAcceptor.Fix;
using FixAcceptor.Infrastructure;

namespace FixAcceptor.Services;

public class FixAcceptorHost : IHostedService
{
    private readonly FixServerApp _app;
    private readonly ISessionSettingsProvider _settingsProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FixAcceptorHost> _logger;
    private ThreadedSocketAcceptor? _acceptor;

    public FixAcceptorHost(
        FixServerApp app,
        ISessionSettingsProvider settingsProvider,
        ILoggerFactory loggerFactory,
        ILogger<FixAcceptorHost> logger)
    {
        _app = app;
        _settingsProvider = settingsProvider;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading QuickFIX config: {Path}", _settingsProvider.ResolvedConfigPath);
        var settings = _settingsProvider.GetSettings();
        LogConfiguredSessions(settings);

        var storeFactory = new FileStoreFactory(settings);

        // Use the ILoggerFactory-aware constructor so QuickFIX/n's session
        // logs (incoming/outgoing/event) flow through the same MEL sink as
        // our own logs instead of being dumped on stdout via ScreenLog.
        _acceptor = new ThreadedSocketAcceptor(
            _app,
            storeFactory,
            settings,
            _loggerFactory,
            messageFactory: null);

        _acceptor.Start();
        _logger.LogInformation("FIX acceptor started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_acceptor is not null)
        {
            _acceptor.Stop();
            _acceptor.Dispose();
            _logger.LogInformation("FIX acceptor stopped");
        }
        return Task.CompletedTask;
    }

    private void LogConfiguredSessions(SessionSettings settings)
    {
        var sessions = settings.GetSessions();
        _logger.LogInformation("Configured FIX sessions: {Count}", sessions.Count);
        foreach (var sessionId in sessions)
        {
            var dict = settings.Get(sessionId);
            var port = dict.Has(SessionSettings.SOCKET_ACCEPT_PORT)
                ? dict.GetString(SessionSettings.SOCKET_ACCEPT_PORT)
                : "?";
            var heartbeat = dict.Has(SessionSettings.HEARTBTINT)
                ? dict.GetString(SessionSettings.HEARTBTINT)
                : "?";
            _logger.LogInformation(
                "  - {SessionId} on port {Port}, HeartBtInt={Heartbeat}",
                sessionId, port, heartbeat);
        }
    }
}
