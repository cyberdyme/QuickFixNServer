using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using FixAcceptor.Models;

namespace FixAcceptor.Services;

public class ExecutionFileProcessor : IExecutionFileProcessor
{
    private readonly ISessionRegistry _sessionRegistry;
    private readonly ILogger<ExecutionFileProcessor> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

        string json;
        try
        {
            json = await File.ReadAllTextAsync(filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {FilePath}", filePath);
            throw;
        }

        ExecutionData[]? executions;
        try
        {
            executions = JsonSerializer.Deserialize<ExecutionData[]>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON from {FilePath}", filePath);
            throw;
        }

        if (executions is null || executions.Length == 0)
        {
            _logger.LogWarning("File {FilePath} contains no executions", filePath);
            return;
        }

        var activeSessions = _sessionRegistry.GetActiveSessions();
        if (activeSessions.Count == 0)
        {
            _logger.LogWarning(
                "File {FilePath} contains {Count} execution(s) but no sessions are logged on. Executions will not be sent.",
                filePath, executions.Length);
            return;
        }

        _logger.LogInformation(
            "Processing {Count} execution(s) from {FilePath} for {SessionCount} active session(s)",
            executions.Length, filePath, activeSessions.Count);

        foreach (var exec in executions)
        {
            if (!ValidateExecution(exec, out var validationError))
            {
                _logger.LogError("Invalid execution in {FilePath}: {Error}", filePath, validationError);
                continue;
            }

            var side = ConvertSide(exec.Side);
            var price = exec.Price ?? 100m;

            var execReport = BuildExecutionReportWithPrice(
                exec.ClOrdID, exec.Symbol, side, exec.OrderQty, price);

            foreach (var sessionId in activeSessions)
            {
                try
                {
                    Session.SendToTarget(execReport, sessionId);
                    _logger.LogInformation(
                        "Sent ExecutionReport to {SessionId}: ClOrdID={ClOrdID}, Symbol={Symbol}, OrderID={OrderID}",
                        sessionId, exec.ClOrdID, exec.Symbol, execReport.OrderID.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send ExecutionReport to {SessionId} for ClOrdID={ClOrdID}",
                        sessionId, exec.ClOrdID);
                }
            }
        }
    }

    public static bool ValidateExecution(ExecutionData exec, out string error)
    {
        if (string.IsNullOrWhiteSpace(exec.ClOrdID))
        {
            error = "ClOrdID is required";
            return false;
        }
        if (string.IsNullOrWhiteSpace(exec.Symbol))
        {
            error = "Symbol is required";
            return false;
        }
        if (exec.OrderQty <= 0)
        {
            error = $"OrderQty must be positive, got: {exec.OrderQty}";
            return false;
        }
        if (!exec.Side.Equals("BUY", StringComparison.OrdinalIgnoreCase) &&
            !exec.Side.Equals("SELL", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Side must be BUY or SELL, got: {exec.Side}";
            return false;
        }
        if (exec.Price.HasValue && exec.Price.Value <= 0)
        {
            error = $"Price must be positive if provided, got: {exec.Price}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static char ConvertSide(string side)
    {
        return side.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            ? Side.BUY
            : Side.SELL;
    }

    public static QuickFix.FIX44.ExecutionReport BuildExecutionReportWithPrice(
        string clOrdId, string symbol, char side, decimal orderQty, decimal price)
    {
        var orderId = Guid.NewGuid().ToString("N");
        var execId = Guid.NewGuid().ToString("N");

        var execReport = new QuickFix.FIX44.ExecutionReport(
            new OrderID(orderId),
            new ExecID(execId),
            new ExecType(ExecType.FILL),
            new OrdStatus(OrdStatus.FILLED),
            new Symbol(symbol),
            new Side(side),
            new LeavesQty(0m),
            new CumQty(orderQty),
            new AvgPx(price));

        execReport.Set(new ClOrdID(clOrdId));
        execReport.Set(new LastQty(orderQty));
        execReport.Set(new LastPx(price));

        return execReport;
    }
}
