using Microsoft.Extensions.Options;
using QuickFix;

namespace FixAcceptor.Infrastructure;

public interface ISessionSettingsProvider
{
    SessionSettings GetSettings();
    string ResolvedConfigPath { get; }
}

public class SessionSettingsProvider : ISessionSettingsProvider
{
    private readonly string _configPath;

    public SessionSettingsProvider(IOptions<FixAcceptorOptions> options)
    {
        _configPath = options.Value.ConfigFile;
    }

    public string ResolvedConfigPath => Path.Combine(AppContext.BaseDirectory, _configPath);

    public SessionSettings GetSettings()
    {
        var fullPath = ResolvedConfigPath;
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"QuickFIX config not found at: {fullPath}");

        return new SessionSettings(fullPath);
    }
}
