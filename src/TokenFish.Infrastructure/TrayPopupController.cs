namespace TokenFish.Infrastructure;

public sealed class TrayPopupController : IDisposable
{
    private readonly IPopupShell _shell;
    private readonly IPopupUpdateTimer _timer;
    private readonly IPopupActionQueue _actionQueue;
    private readonly Func<CancellationToken, Task> _refreshAsync;
    private readonly object _sync = new();
    private bool _disposed;
    private bool _intendedVisible;
    private bool _activationPending;
    private bool _hideInProgress;
    private bool _timerRunning;
    private long _activationGeneration;

    public TrayPopupController(
        IPopupShell shell,
        IPopupUpdateTimer timer,
        Func<CancellationToken, Task> refreshAsync,
        IPopupActionQueue actionQueue)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(refreshAsync);
        ArgumentNullException.ThrowIfNull(actionQueue);

        _shell = shell;
        _timer = timer;
        _refreshAsync = refreshAsync;
        _actionQueue = actionQueue;

        _shell.Activated += OnActivated;
        _shell.Deactivated += OnDeactivated;
        _shell.CloseRequested += OnCloseRequested;
        _timer.Tick += OnTimerTick;
    }

    public async Task ToggleAsync(CancellationToken cancellationToken)
    {
        long generation;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_intendedVisible || _shell.IsVisible)
            {
                generation = 0;
            }
            else
            {
                _intendedVisible = true;
                _activationPending = true;
                generation = ++_activationGeneration;
            }
        }

        if (generation == 0)
        {
            await HideAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await _refreshAsync(cancellationToken).ConfigureAwait(false);
            await _shell.ShowAsync(cancellationToken).ConfigureAwait(false);
            StartTimerIfCurrent(generation);
        }
        catch
        {
            CancelActivation(generation);
            throw;
        }
    }

    public async Task ShowAsync(CancellationToken cancellationToken)
    {
        long generation;
        var newActivation = false;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _intendedVisible = true;
            if (!_shell.IsVisible)
            {
                _activationPending = true;
                generation = ++_activationGeneration;
                newActivation = true;
            }
            else
            {
                generation = _activationGeneration;
            }
        }

        try
        {
            await _refreshAsync(cancellationToken).ConfigureAwait(false);
            await _shell.ShowAsync(cancellationToken).ConfigureAwait(false);
            StartTimerIfCurrent(generation);
        }
        catch
        {
            if (newActivation)
            {
                CancelActivation(generation);
            }

            throw;
        }
    }

    public async Task HideAsync(CancellationToken cancellationToken)
    {
        var stopTimer = false;
        lock (_sync)
        {
            if (_disposed || _hideInProgress)
            {
                return;
            }

            _hideInProgress = true;
            _intendedVisible = false;
            _activationPending = false;
            _activationGeneration++;
            stopTimer = _timerRunning;
            _timerRunning = false;
        }

        try
        {
            if (stopTimer)
            {
                _timer.Stop();
            }

            await _shell.HideAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _hideInProgress = false;
            }
        }
    }

    private void StartTimerIfCurrent(long generation)
    {
        lock (_sync)
        {
            if (_disposed ||
                !_intendedVisible ||
                generation != _activationGeneration ||
                _timerRunning)
            {
                return;
            }

            _timerRunning = true;
        }

        _timer.Start();
    }

    public void Dispose()
    {
        var stopTimer = false;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _intendedVisible = false;
            _activationPending = false;
            _activationGeneration++;
            stopTimer = _timerRunning;
            _timerRunning = false;
        }

        _shell.Activated -= OnActivated;
        _shell.Deactivated -= OnDeactivated;
        _shell.CloseRequested -= OnCloseRequested;
        _timer.Tick -= OnTimerTick;
        if (stopTimer)
        {
            _timer.Stop();
        }
    }

    private void CancelActivation(long generation)
    {
        lock (_sync)
        {
            if (generation != _activationGeneration)
            {
                return;
            }

            _intendedVisible = false;
            _activationPending = false;
            _activationGeneration++;
        }
    }

    private void OnActivated()
    {
        long generation;
        lock (_sync)
        {
            if (_disposed || !_intendedVisible || !_activationPending)
            {
                return;
            }

            generation = _activationGeneration;
        }

        _actionQueue.Enqueue(() => CompleteActivation(generation));
    }

    private void CompleteActivation(long generation)
    {
        lock (_sync)
        {
            if (!_disposed &&
                _intendedVisible &&
                _activationPending &&
                generation == _activationGeneration)
            {
                _activationPending = false;
            }
        }
    }

    private void OnDeactivated()
    {
        long generation;
        lock (_sync)
        {
            if (_disposed || !_intendedVisible || _activationPending)
            {
                return;
            }

            generation = _activationGeneration;
        }

        _actionQueue.Enqueue(() =>
        {
            if (ShouldHideForDeactivation(generation))
            {
                _ = HideAsync(CancellationToken.None);
            }
        });
    }

    private void OnCloseRequested()
    {
        _ = HideAsync(CancellationToken.None);
    }

    private void OnTimerTick()
    {
        var shouldRefresh = false;
        lock (_sync)
        {
            shouldRefresh = !_disposed && _intendedVisible && _shell.IsVisible;
        }

        if (shouldRefresh)
        {
            _ = _refreshAsync(CancellationToken.None);
        }
    }

    private bool ShouldHideForDeactivation(long generation)
    {
        lock (_sync)
        {
            return !_disposed &&
                _intendedVisible &&
                !_activationPending &&
                !_hideInProgress &&
                generation == _activationGeneration &&
                _shell.IsVisible &&
                !_shell.IsForeground;
        }
    }
}
