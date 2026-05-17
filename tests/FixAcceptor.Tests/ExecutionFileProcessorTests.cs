using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using QuickFix;
using QuickFix.Fields;
using FixAcceptor.Models;
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
    public async Task ProcessFileAsync_ValidJson_NoErrorsLogged()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var executions = new[]
            {
                new ExecutionData
                {
                    ClOrdID = "ORD001", Symbol = "AAPL", Side = "BUY",
                    OrderQty = 100, Price = 150.25m
                }
            };
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(executions));

            _registryMock.Setup(r => r.GetActiveSessions())
                .Returns(new List<SessionID> { new("FIX.4.4", "SERVER", "CLIENT") });

            // SendToTarget will fail because no real session exists, but that's caught internally
            await _processor.ProcessFileAsync(tempFile);

            // Verify no validation errors were logged
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Invalid execution")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessFileAsync_InvalidJson_ThrowsJsonException()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "{ not valid json array ]]]");

            await Assert.ThrowsAsync<JsonException>(() => _processor.ProcessFileAsync(tempFile));
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
            var executions = new[]
            {
                new ExecutionData
                {
                    ClOrdID = "ORD001", Symbol = "AAPL", Side = "BUY", OrderQty = 100
                }
            };
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(executions));

            _registryMock.Setup(r => r.GetActiveSessions())
                .Returns(new List<SessionID>());

            await _processor.ProcessFileAsync(tempFile);

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("no sessions are logged on")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("", "AAPL", "BUY", 100)]      // missing ClOrdID
    [InlineData("ORD1", "", "BUY", 100)]       // missing Symbol
    [InlineData("ORD1", "AAPL", "INVALID", 100)] // bad Side
    [InlineData("ORD1", "AAPL", "BUY", 0)]    // zero qty
    [InlineData("ORD1", "AAPL", "BUY", -5)]   // negative qty
    public async Task ProcessFileAsync_InvalidExecution_LogsErrorAndContinues(
        string clOrdId, string symbol, string side, decimal qty)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var executions = new[]
            {
                new ExecutionData { ClOrdID = clOrdId, Symbol = symbol, Side = side, OrderQty = qty }
            };
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(executions));

            _registryMock.Setup(r => r.GetActiveSessions())
                .Returns(new List<SessionID> { new("FIX.4.4", "SERVER", "CLIENT") });

            await _processor.ProcessFileAsync(tempFile);

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Invalid execution")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessFileAsync_MissingPrice_DefaultsTo100()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var executions = new[]
            {
                new ExecutionData
                {
                    ClOrdID = "ORD001", Symbol = "AAPL", Side = "BUY", OrderQty = 100
                    // Price intentionally omitted → defaults to 100
                }
            };
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(executions));

            _registryMock.Setup(r => r.GetActiveSessions())
                .Returns(new List<SessionID> { new("FIX.4.4", "SERVER", "CLIENT") });

            await _processor.ProcessFileAsync(tempFile);

            // No validation errors
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Invalid execution")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region BuildExecutionReportWithPrice tests

    [Fact]
    public void BuildExecutionReportWithPrice_SetsCustomPrice()
    {
        var report = ExecutionFileProcessor.BuildExecutionReportWithPrice(
            "ORD1", "AAPL", Side.BUY, 100m, 155.50m);

        Assert.Equal(155.50m, report.AvgPx.Value);
        Assert.Equal(155.50m, report.LastPx.Value);
        Assert.Equal(100m, report.CumQty.Value);
        Assert.Equal(100m, report.LastQty.Value);
        Assert.Equal(0m, report.LeavesQty.Value);
        Assert.Equal("ORD1", report.ClOrdID.Value);
        Assert.Equal("AAPL", report.Symbol.Value);
        Assert.Equal(Side.BUY, report.Side.Value);
        Assert.Equal(ExecType.FILL, report.ExecType.Value);
        Assert.Equal(OrdStatus.FILLED, report.OrdStatus.Value);
    }

    #endregion

    #region Validation tests

    [Fact]
    public void ValidateExecution_ValidData_ReturnsTrue()
    {
        var exec = new ExecutionData
        {
            ClOrdID = "ORD1", Symbol = "AAPL", Side = "BUY", OrderQty = 100
        };

        Assert.True(ExecutionFileProcessor.ValidateExecution(exec, out var error));
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void ValidateExecution_NegativePrice_ReturnsFalse()
    {
        var exec = new ExecutionData
        {
            ClOrdID = "ORD1", Symbol = "AAPL", Side = "BUY", OrderQty = 100, Price = -50m
        };

        Assert.False(ExecutionFileProcessor.ValidateExecution(exec, out var error));
        Assert.Contains("Price must be positive", error);
    }

    [Theory]
    [InlineData("buy")]
    [InlineData("BUY")]
    [InlineData("Buy")]
    public void ConvertSide_BuyCaseInsensitive_ReturnsBuy(string side)
    {
        Assert.Equal(Side.BUY, ExecutionFileProcessor.ConvertSide(side));
    }

    [Theory]
    [InlineData("sell")]
    [InlineData("SELL")]
    [InlineData("Sell")]
    public void ConvertSide_SellCaseInsensitive_ReturnsSell(string side)
    {
        Assert.Equal(Side.SELL, ExecutionFileProcessor.ConvertSide(side));
    }

    #endregion
}
