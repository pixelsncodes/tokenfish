namespace TokenFish.Infrastructure;

public sealed class TrayPopupController : IDisposable
{
    private readonly IPopupShell _shell;
    private readonly IPopupUpdateTimer _timer;
    private readonly Func<CancellationToken, Task> _refreshAsync;
    private bool _disposed;

    public TrayPopupController(
        IPopupShell shell,
        IPopupUpdateTimer timer,
        Func<CancellationToken, Task> refreshAsync)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(refreshAsync);

        _shell = shell;
        _timer = timer;
        _refreshAsync = refreshAsync;

        _shell.Deactivated += OnDeactivated;
        _shell.CloseRequested += OnCloseRequested;
        _timer.Tick += OnTimerTick;
    }

    public async Task ToggleAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        if (_shell.IsVisible)
        {
            await HideAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await _refreshAsync(cancellationToken).ConfigureAwait(false);
        await _shell.ShowAsync(cancellationToken).ConfigureAwait(false);
        _timer.Start();
    }

    public async Task HideAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        await _shell.HideAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shell.Deactivated -= OnDeactivated;
        _shell.CloseRequested -= OnCloseRequested;
        _timer.Tick -= OnTimerTick;
        _timer.Stop();
    }

    private void OnDeactivated()
    {
        _ = HideAsync(CancellationToken.None);
    }

    private void OnCloseRequested()
    {
        _ = HideAsync(CancellationToken.None);
    }

    private void OnTimerTick()
    {
        if (!_disposed && _shell.IsVisible)
        {
            _ = _refreshAsync(CancellationToken.None);
        }
    }
}
