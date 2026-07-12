namespace TokenFish.Infrastructure;

public sealed class ApplicationRelaunchActivationController : IDisposable
{
    private readonly IApplicationRelaunchActivationSource _activationSource;
    private readonly IPopupActionQueue _actionQueue;
    private readonly Func<CancellationToken, Task> _showPopupAsync;
    private readonly Action _reportActivationFault;
    private readonly object _sync = new();

    private IDisposable? _subscription;
    private bool _initialized;
    private bool _activationQueued;
    private bool _activationInProgress;
    private bool _shutdownStarted;
    private bool _disposed;

    public ApplicationRelaunchActivationController(
        IApplicationRelaunchActivationSource activationSource,
        IPopupActionQueue actionQueue,
        Func<CancellationToken, Task> showPopupAsync,
        Action reportActivationFault)
    {
        ArgumentNullException.ThrowIfNull(activationSource);
        ArgumentNullException.ThrowIfNull(actionQueue);
        ArgumentNullException.ThrowIfNull(showPopupAsync);
        ArgumentNullException.ThrowIfNull(reportActivationFault);

        _activationSource = activationSource;
        _actionQueue = actionQueue;
        _showPopupAsync = showPopupAsync;
        _reportActivationFault = reportActivationFault;
    }

    public void Initialize()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_initialized)
            {
                return;
            }

            _initialized = true;
        }

        _subscription = _activationSource.SubscribeActivated(OnActivated);
    }

    public void BeginShutdown()
    {
        IDisposable? subscription;
        lock (_sync)
        {
            _shutdownStarted = true;
            _activationQueued = false;
            subscription = _subscription;
            _subscription = null;
        }

        subscription?.Dispose();
    }

    public void Dispose()
    {
        IDisposable? subscription;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdownStarted = true;
            _activationQueued = false;
            subscription = _subscription;
            _subscription = null;
        }

        subscription?.Dispose();
    }

    private void OnActivated()
    {
        lock (_sync)
        {
            if (_disposed ||
                _shutdownStarted ||
                _activationQueued ||
                _activationInProgress)
            {
                return;
            }

            _activationQueued = true;
        }

        _actionQueue.Enqueue(() => _ = ShowQueuedActivationAsync());
    }

    private async Task ShowQueuedActivationAsync()
    {
        lock (_sync)
        {
            if (_disposed || _shutdownStarted || !_activationQueued)
            {
                _activationQueued = false;
                return;
            }

            _activationQueued = false;
            _activationInProgress = true;
        }

        try
        {
            await _showPopupAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!IsShutdownStarted())
            {
                _reportActivationFault();
            }
        }
        catch
        {
            if (!IsShutdownStarted())
            {
                _reportActivationFault();
            }
        }
        finally
        {
            lock (_sync)
            {
                _activationInProgress = false;
            }
        }
    }

    private bool IsShutdownStarted()
    {
        lock (_sync)
        {
            return _shutdownStarted || _disposed;
        }
    }
}
