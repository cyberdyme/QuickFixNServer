using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Logger;
using QuickFix.Store;
using FixAcceptor.Fix;
using FixAcceptor.Infrastructure;

namespace FixAcceptor.Services;

public class FixAcceptorHost : IHostedService
{
    private readonly FixServerApp _app;
    private readonly ISessionSettingsProvider _settingsProvider;
    private readonly ILogger<FixAcceptorHost> _logger;
    private ThreadedSocketAcceptor? _acceptor;

    public FixAcceptorHost(
        FixServerApp app,
        ISessionSettingsProvider settingsProvider,
        ILogger<FixAcceptorHost> logger)
    {
        _app = app;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsProvider.GetSettings();
        var storeFactory = new FileStoreFactory(settings);
        var logFactory = new ScreenLogFactory(settings);

        _acceptor = new ThreadedSocketAcceptor(
            _app,
            storeFactory,
            settings,
            logFactory,
            messageFactory: null);

        _acceptor.Start();
        _logger.LogInformation("FIX acceptor started on port 5001");
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
}
