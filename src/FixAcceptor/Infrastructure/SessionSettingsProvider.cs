using QuickFix;

namespace FixAcceptor.Infrastructure;

public interface ISessionSettingsProvider
{
    SessionSettings GetSettings();
}

public class SessionSettingsProvider : ISessionSettingsProvider
{
    private readonly string _configPath;

    public SessionSettingsProvider(string configPath = "server.cfg")
    {
        _configPath = configPath;
    }

    public SessionSettings GetSettings()
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, _configPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"QuickFIX config not found at: {fullPath}");

        return new SessionSettings(fullPath);
    }
}
