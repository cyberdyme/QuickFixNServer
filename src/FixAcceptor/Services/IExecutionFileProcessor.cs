namespace FixAcceptor.Services;

public interface IExecutionFileProcessor
{
    Task ProcessFileAsync(string filePath, CancellationToken cancellationToken = default);
}
