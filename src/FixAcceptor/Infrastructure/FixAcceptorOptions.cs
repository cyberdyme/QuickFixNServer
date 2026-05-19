namespace FixAcceptor.Infrastructure;

/// <summary>
/// C#-owned configuration for the acceptor host. The FIX session itself is
/// configured by server.cfg (QuickFIX/n format); these are settings that
/// belong to the .NET host, not the FIX engine.
/// </summary>
public class FixAcceptorOptions
{
    public const string SectionName = "FixAcceptor";

    /// <summary>
    /// Path to the QuickFIX/n session config file, resolved relative to the
    /// app's BaseDirectory. Defaults to "server.cfg".
    /// </summary>
    public string ConfigFile { get; set; } = "server.cfg";

    /// <summary>
    /// Directory the ExecutionFileWatcherService watches for raw-FIX files to
    /// replay onto active sessions. Resolved relative to BaseDirectory.
    /// </summary>
    public string ExecutionsDirectory { get; set; } = "executions";

    /// <summary>
    /// Sub-directory under ExecutionsDirectory where processed files are
    /// archived after replay. Resolved relative to BaseDirectory.
    /// </summary>
    public string ProcessedDirectory { get; set; } = "executions/processed";

    /// <summary>
    /// How often the MarketDataPublisher emits an IncrementalRefresh per
    /// active subscription, in seconds.
    /// </summary>
    public double MarketDataTickIntervalSeconds { get; set; } = 1.0;
}
