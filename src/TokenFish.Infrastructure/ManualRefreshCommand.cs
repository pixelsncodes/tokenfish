using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public sealed class ManualRefreshCommand : IDisposable
{
    private readonly IApplicationRuntimeHost _runtimeHost;
    private readonly object _sync = new();

    private ProviderRefreshStatus _refreshStatus;
    private ManualRefreshCommandState _state;
    private bool _requestInProgress;
    private bool _disposed;

    public ManualRefreshCommand(IApplicationRuntimeHost runtimeHost)
    {
        _runtimeHost = runtimeHost ?? throw new ArgumentNullException(nameof(runtimeHost));
        _refreshStatus = runtimeHost.RefreshStatus;
        _state = CreateState(_refreshStatus, _requestInProgress);
        _runtimeHost.RefreshStatusChanged += OnRefreshStatusChanged;
    }

    public event Action<ManualRefreshCommandState>? StateChanged;

    public ManualRefreshCommandState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public async Task RequestAsync(CancellationToken cancellationToken)
    {
        ManualRefreshCommandState? changedState;
        lock (_sync)
        {
            if (_disposed ||
                _requestInProgress ||
                _refreshStatus.IsRefreshActive)
            {
                return;
            }

            _requestInProgress = true;
            changedState = SetStateUnderLock(CreateState(_refreshStatus, _requestInProgress));
        }

        NotifyStateChanged(changedState);

        try
        {
            await _runtimeHost.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            changedState = null;
            lock (_sync)
            {
                if (!_disposed)
                {
                    _requestInProgress = false;
                    changedState = SetStateUnderLock(CreateState(_refreshStatus, _requestInProgress));
                }
            }

            NotifyStateChanged(changedState);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _runtimeHost.RefreshStatusChanged -= OnRefreshStatusChanged;
    }

    private void OnRefreshStatusChanged(ProviderRefreshStatus status)
    {
        ManualRefreshCommandState? changedState;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _refreshStatus = status;
            changedState = SetStateUnderLock(CreateState(_refreshStatus, _requestInProgress));
        }

        NotifyStateChanged(changedState);
    }

    private ManualRefreshCommandState? SetStateUnderLock(ManualRefreshCommandState state)
    {
        if (_state == state)
        {
            return null;
        }

        _state = state;
        return state;
    }

    private void NotifyStateChanged(ManualRefreshCommandState? state)
    {
        if (state is null)
        {
            return;
        }

        var handlers = StateChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<ManualRefreshCommandState> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(state);
            }
            catch
            {
            }
        }
    }

    private static ManualRefreshCommandState CreateState(
        ProviderRefreshStatus refreshStatus,
        bool requestInProgress) =>
        requestInProgress || refreshStatus.IsRefreshActive
            ? ManualRefreshCommandState.Refreshing
            : ManualRefreshCommandState.Available;
}
