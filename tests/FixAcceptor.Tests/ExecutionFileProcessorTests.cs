using Microsoft.Extensions.Logging;
using Moq;
using QuickFix;
using FixAcceptor.Services;

namespace FixAcceptor.Tests;

public class ExecutionFileProcessorTests
{
    private readonly Mock<ISessionRegistry> _registryMock;
    private readonly Mock<ILogger<ExecutionFileProcessor>> _loggerMock;
    private readonly ExecutionFileProcessor _processor;

    public ExecutionFileProcessorTests()
    {
        _registryMock = new Mock<ISessionRegistry>();
        _loggerMock = new Mock<ILogger<ExecutionFileProcessor>>();
        _processor = new ExecutionFileProcessor(_registryMock.Object, _loggerMock.Object);
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
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region NormalizeFixLine tests

    [Fact]
    public void NormalizeFixLine_PipeDelimited_ReplacesWithSoh()
    {
        //  (fixed-length unicode escape) — NOT \x01 in strings, which greedily eats following hex.
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
        // Body length and checksum are not strictly validated by the Message string constructor,
        // but we include realistic values so the line round-trips through parsing cleanly.
        var body =
            $"35=8|49=SERVER|56=CLIENT|34=1|52=20260517-12:00:00.000|" +
            $"37=ORDER1|11={clOrdId}|17=EXEC1|150=F|39=2|55=AAPL|54=1|" +
            $"151=0|14=100|6=100|31=100|32=100|";
        var msg = $"8=FIX.4.4|9={body.Length}|{body}10=000|";
        return msg;
    }
}
