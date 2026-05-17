using Microsoft.Extensions.Logging;
using QuickFix;

namespace FixAcceptor.Services;

public class ExecutionFileProcessor : IExecutionFileProcessor
{
    private readonly ISessionRegistry _sessionRegistry;
    private readonly ILogger<ExecutionFileProcessor> _logger;

    public ExecutionFileProcessor(
        ISessionRegistry sessionRegistry,
        ILogger<ExecutionFileProcessor> logger)
    {
        _sessionRegistry = sessionRegistry;
        _logger = logger;
    }

    public async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reading execution file: {FilePath}", filePath);

        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {FilePath}", filePath);
            throw;
        }

        var messages = lines
            .Select(NormalizeFixLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (messages.Length == 0)
        {
            _logger.LogWarning("File {FilePath} contains no FIX messages", filePath);
            return;
        }

        var activeSessions = _sessionRegistry.GetActiveSessions();
        if (activeSessions.Count == 0)
        {
            _logger.LogWarning(
                "File {FilePath} contains {Count} message(s) but no sessions are logged on. Messages will not be sent.",
                filePath, messages.Length);
            return;
        }

        _logger.LogInformation(
            "Processing {Count} FIX message(s) from {FilePath} for {SessionCount} active session(s)",
            messages.Length, filePath, activeSessions.Count);

        foreach (var raw in messages)
        {
            Message parsed;
            try
            {
                // validate=false: BodyLength/CheckSum are recalculated by the engine on send,
                // so stale values in hand-rolled or replayed files don't reject the message.
                parsed = new Message(raw, validate: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse FIX message from {FilePath}: {Raw}", filePath, raw);
                continue;
            }

            foreach (var sessionId in activeSessions)
            {
                try
                {
                    if (Session.SendToTarget(parsed, sessionId))
                    {
                        _logger.LogInformation("Sent FIX message to {SessionId}", sessionId);
                    }
                    else
                    {
                        _logger.LogWarning("Session {SessionId} refused to send message", sessionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send FIX message to {SessionId}", sessionId);
                }
            }
        }
    }

    // Accept pipe-delimited FIX dumps as a friendlier alternative to wire-format SOH.
    public static string NormalizeFixLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        return line.Contains('|') && !line.Contains('\x01')
            ? line.Replace('|', '\x01')
            : line;
    }
}
