namespace TokenFish.Infrastructure;

public sealed class ApplicationShutdownCoordinator
{
    private readonly Func<CancellationToken, Task> _cleanupAsync;
    private readonly Action _completeApplicationShutdown;
    private readonly Action _reportShutdownFault;
    private readonly object _sync = new();

    private Task? _shutdownTask;

    public ApplicationShutdownCoordinator(
        Func<CancellationToken, Task> cleanupAsync,
        Action completeApplicationShutdown,
        Action reportShutdownFault)
    {
        ArgumentNullException.ThrowIfNull(cleanupAsync);
        ArgumentNullException.ThrowIfNull(completeApplicationShutdown);
        ArgumentNullException.ThrowIfNull(reportShutdownFault);

        _cleanupAsync = cleanupAsync;
        _completeApplicationShutdown = completeApplicationShutdown;
        _reportShutdownFault = reportShutdownFault;
    }

    public bool IsShutdownStarted
    {
        get
        {
            lock (_sync)
            {
                return _shutdownTask is not null;
            }
        }
    }

    public Task ShutdownAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _shutdownTask ??= ShutdownCoreAsync();

            return cancellationToken.CanBeCanceled
                ? _shutdownTask.WaitAsync(cancellationToken)
                : _shutdownTask;
        }
    }

    private async Task ShutdownCoreAsync()
    {
        try
        {
            await _cleanupAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            _reportShutdownFault();
        }
        finally
        {
            _completeApplicationShutdown();
        }
    }
}
