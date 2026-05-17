using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixAcceptor.Services;

public class ExecutionFileWatcherService : BackgroundService
{
    private readonly IExecutionFileProcessor _processor;
    private readonly ILogger<ExecutionFileWatcherService> _logger;
    private readonly string _watchDirectory;
    private readonly string _processedDirectory;
    private FileSystemWatcher? _watcher;

    public ExecutionFileWatcherService(
        IExecutionFileProcessor processor,
        ILogger<ExecutionFileWatcherService> logger)
    {
        _processor = processor;
        _logger = logger;
        _watchDirectory = Path.Combine(AppContext.BaseDirectory, "executions");
        _processedDirectory = Path.Combine(_watchDirectory, "processed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EnsureDirectoriesExist();

        _watcher = new FileSystemWatcher(_watchDirectory)
        {
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += (sender, e) =>
            _ = OnFileCreatedAsync(e.FullPath, stoppingToken);

        _logger.LogInformation(
            "ExecutionFileWatcherService started — watching: {WatchDirectory}",
            _watchDirectory);

        return Task.CompletedTask;
    }

    private async Task OnFileCreatedAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!await WaitForFileReadyAsync(filePath, cancellationToken))
            return;

        try
        {
            _logger.LogInformation("Detected new execution file: {FilePath}", filePath);
            await _processor.ProcessFileAsync(filePath, cancellationToken);
            MoveToProcessed(filePath);
            _logger.LogInformation("File processed and archived: {FileName}", Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing file {FilePath}. File will NOT be moved so it can be inspected.",
                filePath);
        }
    }

    private async Task<bool> WaitForFileReadyAsync(string filePath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        const int delayMs = 100;

        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                if (i == maxAttempts - 1)
                {
                    _logger.LogWarning(
                        "File {FilePath} still locked after {Attempts} attempts — skipping",
                        filePath, maxAttempts);
                    return false;
                }
                await Task.Delay(delayMs, cancellationToken);
            }
        }
        return false;
    }

    private void MoveToProcessed(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var destPath = Path.Combine(_processedDirectory, $"{fileName}_{timestamp}{extension}");

        File.Move(filePath, destPath, overwrite: true);
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_watchDirectory);
        Directory.CreateDirectory(_processedDirectory);
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}
