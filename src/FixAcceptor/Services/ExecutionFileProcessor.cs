using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace FixAcceptor.Services;

public class ExecutionFileProcessor : IExecutionFileProcessor
{
    private readonly ISessionRegistry _sessionRegistry;
    private readonly IFixMessageSender _sender;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExecutionFileProcessor> _logger;

    public ExecutionFileProcessor(
        ISessionRegistry sessionRegistry,
        IFixMessageSender sender,
        TimeProvider timeProvider,
        ILogger<ExecutionFileProcessor> logger)
    {
        _sessionRegistry = sessionRegistry;
        _sender = sender;
        _timeProvider = timeProvider;
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

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var raw in messages)
        {
            Message parsed;
            try
            {
                // validate=false because the file's BodyLength/CheckSum/MsgSeqNum
                // are stale — we strip and let the engine recompute on send.
                parsed = new Message(raw, validate: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse FIX message from {FilePath}: {Raw}", filePath, raw);
                continue;
            }

            PrepareForReplay(parsed, nowUtc);

            foreach (var sessionId in activeSessions)
            {
                try
                {
                    if (_sender.SendToTarget(parsed, sessionId))
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

    /// <summary>
    /// Prepare a parsed-from-file FIX message for replay onto a live session.
    ///
    /// We strip the four header/trailer fields that the session engine owns
    /// (MsgSeqNum, SendingTime, BodyLength, CheckSum) so the file's stale
    /// values can't leak onto the wire. QuickFIX/n's Session.SendToTarget
    /// fills in MsgSeqNum from the session's outbound sequence (giving the
    /// client the next number it expects) and recomputes BodyLength and
    /// CheckSum on serialization.
    ///
    /// For trade-bearing message types we additionally stamp TransactTime
    /// (tag 60) and TradeDate (tag 75) to "now / today" so the trade
    /// appears as having happened today rather than whenever the file
    /// was authored. Other message types are left untouched.
    /// </summary>
    public static void PrepareForReplay(Message message, DateTime nowUtc)
    {
        // Defensive: clear engine-owned fields. Session.SendToTarget already
        // overwrites MsgSeqNum and SendingTime on send, and the wire encoder
        // recomputes BodyLength and CheckSum, but removing the stale values
        // here keeps the intent explicit and the message object self-consistent
        // if any code reads it before send.
        message.Header.RemoveField(Tags.MsgSeqNum);
        message.Header.RemoveField(Tags.SendingTime);
        message.Header.RemoveField(Tags.BodyLength);
        message.Trailer.RemoveField(Tags.CheckSum);

        if (IsTradeBearingMessage(SafeGetMsgType(message)))
        {
            // TransactTime is the moment the trade event occurred; TradeDate
            // is the business date for clearing/settlement. Both need to be
            // "today" so a replayed file isn't rejected for stale dates and
            // shows up in the client's blotter under today's tape.
            message.SetField(new TransactTime(nowUtc));
            message.SetField(new TradeDate(nowUtc.ToString("yyyyMMdd")));
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

    private static bool IsTradeBearingMessage(string msgType) => msgType is
        "8"   // ExecutionReport
        or "AE" // TradeCaptureReport
        or "AK" // Confirmation
        or "AS" // AllocationReport
        or "AT"; // AllocationReportAck

    private static string SafeGetMsgType(Message message)
    {
        try { return message.Header.GetString(Tags.MsgType); }
        catch { return "<unknown>"; }
    }
}
