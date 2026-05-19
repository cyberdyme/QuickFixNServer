using Microsoft.Extensions.Logging;
using Moq;
using QuickFix;
using QuickFix.Fields;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class ExecutionFileProcessorTests
{
    private readonly Mock<ISessionRegistry> _registryMock;
    private readonly Mock<ILogger<ExecutionFileProcessor>> _loggerMock;
    private readonly RecordingSender _sender;
    private readonly FakeTimeProvider _clock;
    private readonly ExecutionFileProcessor _processor;

    public ExecutionFileProcessorTests()
    {
        _registryMock = new Mock<ISessionRegistry>();
        _loggerMock = new Mock<ILogger<ExecutionFileProcessor>>();
        _sender = new RecordingSender();
        _clock = new FakeTimeProvider(new DateTime(2026, 5, 20, 14, 30, 0, DateTimeKind.Utc));
        _processor = new ExecutionFileProcessor(
            _registryMock.Object, _sender, _clock, _loggerMock.Object);
    }

    #region ProcessFileAsync tests

    [Fact]
    public async Task ProcessFileAsync_EmptyFile_LogsWarning()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "");

            await _processor.ProcessFileAsync(tempFile);

            VerifyLog(LogLevel.Warning, "contains no FIX messages", Times.Once());
            _registryMock.Verify(r => r.GetActiveSessions(), Times.Never);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessFileAsync_OnlyBlankLines_LogsWarning()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "\n   \n\t\n");

            await _processor.ProcessFileAsync(tempFile);

            VerifyLog(LogLevel.Warning, "contains no FIX messages", Times.Once());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessFileAsync_NoActiveSessions_LogsWarning()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, SampleExecutionReport());

            _registryMock.Setup(r => r.GetActiveSessions())
                .Returns(new List<SessionID>());

            await _processor.ProcessFileAsync(tempFile);

            VerifyLog(LogLevel.Warning, "no sessions are logged on", Times.Once());
            Assert.Empty(_sender.Sent);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessFileAsync_MalformedLine_LogsParseError()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "this is not a fix message at all");

            _registryMock.Setup(r => r.GetActiveSessions())
                .Returns(new List<SessionID> { new("FIX.4.4", "SERVER", "CLIENT") });

            await _processor.ProcessFileAsync(tempFile);

            VerifyLog(LogLevel.Error, "Failed to parse FIX message", Times.Once());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessFileAsync_MultipleLines_ParsesEach()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var content = string.Join("\n",
                SampleExecutionReport(clOrdId: "ORD1"),
                "",
                SampleExecutionReport(clOrdId: "ORD2"),
                "   ");
            await File.WriteAllTextAsync(tempFile, content);

            _registryMock.Setup(r => r.GetActiveSessions())
                .Returns(new List<SessionID> { new("FIX.4.4", "SERVER", "CLIENT") });

            await _processor.ProcessFileAsync(tempFile);

            // Two valid lines, two blank — only the two valid ones should be processed.
            VerifyLog(LogLevel.Information, "Processing 2 FIX message(s)", Times.Once());
            VerifyLog(LogLevel.Error, "Failed to parse FIX message", Times.Never());
            Assert.Equal(2, _sender.Sent.Count);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessFileAsync_SendsMessagesWithFreshTradeTimestamps()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // File-authored timestamp is days old — replay should not propagate it.
            await File.WriteAllTextAsync(tempFile, SampleExecutionReport(clOrdId: "ORD-Z"));

            _registryMock.Setup(r => r.GetActiveSessions())
                .Returns(new List<SessionID> { new("FIX.4.4", "SERVER", "CLIENT") });

            await _processor.ProcessFileAsync(tempFile);

            var sent = Assert.Single(_sender.Sent).message;

            // Header/trailer fields the engine should repopulate must not carry
            // values from the file.
            Assert.False(sent.Header.IsSetField(Tags.MsgSeqNum));
            Assert.False(sent.Header.IsSetField(Tags.SendingTime));
            Assert.False(sent.Header.IsSetField(Tags.BodyLength));
            Assert.False(sent.Trailer.IsSetField(Tags.CheckSum));

            // Trade timestamps must reflect "today" per the injected clock.
            Assert.True(sent.IsSetField(Tags.TransactTime));
            Assert.True(sent.IsSetField(Tags.TradeDate));
            Assert.Equal("20260520", sent.GetString(Tags.TradeDate));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region PrepareForReplay tests

    [Fact]
    public void PrepareForReplay_StripsEngineOwnedHeaderAndTrailerFields()
    {
        var raw = SampleExecutionReport().Replace('|', '\x01');
        var msg = new Message(raw, validate: false);
        // Sanity: the file-authored fields are present before prep.
        Assert.True(msg.Header.IsSetField(Tags.MsgSeqNum));
        Assert.True(msg.Header.IsSetField(Tags.SendingTime));

        ExecutionFileProcessor.PrepareForReplay(msg, _clock.GetUtcNow().UtcDateTime);

        Assert.False(msg.Header.IsSetField(Tags.MsgSeqNum));
        Assert.False(msg.Header.IsSetField(Tags.SendingTime));
        Assert.False(msg.Header.IsSetField(Tags.BodyLength));
        Assert.False(msg.Trailer.IsSetField(Tags.CheckSum));
    }

    [Fact]
    public void PrepareForReplay_OnExecutionReport_StampsTransactTimeAndTradeDate()
    {
        var raw = SampleExecutionReport().Replace('|', '\x01');
        var msg = new Message(raw, validate: false);

        var now = new DateTime(2026, 5, 20, 14, 30, 0, DateTimeKind.Utc);
        ExecutionFileProcessor.PrepareForReplay(msg, now);

        Assert.True(msg.IsSetField(Tags.TransactTime));
        Assert.True(msg.IsSetField(Tags.TradeDate));
        Assert.Equal("20260520", msg.GetString(Tags.TradeDate));
    }

    [Fact]
    public void PrepareForReplay_OverwritesStaleTransactTime()
    {
        // Build an ExecutionReport that already carries a stale TransactTime (60).
        var staleBody =
            "35=8|49=SERVER|56=CLIENT|34=1|52=20240101-00:00:00.000|" +
            "60=20240101-00:00:00.000|" +
            "37=ORDER1|11=C1|17=EXEC1|150=F|39=2|55=AAPL|54=1|" +
            "151=0|14=100|6=100|31=100|32=100|";
        var raw = ("8=FIX.4.4|9=" + staleBody.Length + "|" + staleBody + "10=000|").Replace('|', '\x01');
        var msg = new Message(raw, validate: false);

        var now = new DateTime(2026, 5, 20, 14, 30, 0, DateTimeKind.Utc);
        ExecutionFileProcessor.PrepareForReplay(msg, now);

        // The file's 2024-01-01 TransactTime must not survive.
        var fresh = msg.GetString(Tags.TransactTime);
        Assert.DoesNotContain("20240101", fresh);
        Assert.StartsWith("20260520", fresh);
    }

    [Fact]
    public void PrepareForReplay_OnNonTradeMessage_DoesNotAddTransactTime()
    {
        // A NewOrderSingle (35=D) is an order message, not a trade report; we
        // must not stamp it with our own TransactTime / TradeDate.
        var body = "35=D|49=CLIENT|56=SERVER|34=1|52=20240101-00:00:00.000|" +
                   "11=ORD1|55=AAPL|54=1|38=100|40=2|";
        var raw = ("8=FIX.4.4|9=" + body.Length + "|" + body + "10=000|").Replace('|', '\x01');
        var msg = new Message(raw, validate: false);

        ExecutionFileProcessor.PrepareForReplay(msg, new DateTime(2026, 5, 20, 14, 30, 0, DateTimeKind.Utc));

        Assert.False(msg.IsSetField(Tags.TransactTime));
        Assert.False(msg.IsSetField(Tags.TradeDate));
    }

    [Fact]
    public void PrepareForReplay_OnTradeCaptureReport_StampsTransactTimeAndTradeDate()
    {
        var body = "35=AE|49=SERVER|56=CLIENT|34=1|52=20240101-00:00:00.000|" +
                   "571=TR1|487=0|17=EXEC1|150=F|55=AAPL|54=1|31=100|32=100|";
        var raw = ("8=FIX.4.4|9=" + body.Length + "|" + body + "10=000|").Replace('|', '\x01');
        var msg = new Message(raw, validate: false);

        var now = new DateTime(2026, 5, 20, 14, 30, 0, DateTimeKind.Utc);
        ExecutionFileProcessor.PrepareForReplay(msg, now);

        Assert.True(msg.IsSetField(Tags.TransactTime));
        Assert.Equal("20260520", msg.GetString(Tags.TradeDate));
    }

    #endregion

    #region NormalizeFixLine tests

    [Fact]
    public void NormalizeFixLine_PipeDelimited_ReplacesWithSoh()
    {
        var input = "8=FIX.4.4|35=8|10=000";
        var normalized = ExecutionFileProcessor.NormalizeFixLine(input);

        Assert.Equal("8=FIX.4.435=810=000", normalized);
    }

    [Fact]
    public void NormalizeFixLine_AlreadySoh_LeavesAsIs()
    {
        var input = "8=FIX.4.435=810=000";
        var normalized = ExecutionFileProcessor.NormalizeFixLine(input);

        Assert.Equal(input, normalized);
    }

    [Fact]
    public void NormalizeFixLine_MixedSohAndPipe_LeavesAsIs()
    {
        // When SOH is already present, pipes inside values are preserved unchanged.
        var input = "8=FIX.4.458=note|with|pipes10=000";
        var normalized = ExecutionFileProcessor.NormalizeFixLine(input);

        Assert.Equal(input, normalized);
    }

    [Fact]
    public void NormalizeFixLine_Empty_ReturnsEmpty()
    {
        Assert.Equal("", ExecutionFileProcessor.NormalizeFixLine(""));
    }

    #endregion

    private void VerifyLog(LogLevel level, string fragment, Times times)
    {
        _loggerMock.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(fragment)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    // Minimal well-formed FIX 4.4 ExecutionReport using pipe delimiters (NormalizeFixLine swaps for SOH).
    private static string SampleExecutionReport(string clOrdId = "CLORD1")
    {
        var body =
            $"35=8|49=SERVER|56=CLIENT|34=1|52=20260517-12:00:00.000|" +
            $"37=ORDER1|11={clOrdId}|17=EXEC1|150=F|39=2|55=AAPL|54=1|" +
            $"151=0|14=100|6=100|31=100|32=100|";
        var msg = $"8=FIX.4.4|9={body.Length}|{body}10=000|";
        return msg;
    }

    private class RecordingSender : IFixMessageSender
    {
        public List<(Message message, SessionID sessionId)> Sent { get; } = new();
        public bool SendToTarget(Message message, SessionID sessionId)
        {
            Sent.Add((message, sessionId));
            return true;
        }
    }

    private class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
