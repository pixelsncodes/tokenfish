using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public sealed class NotificationAreaController : IDisposable
{
    private readonly INotificationAreaIcon _icon;
    private readonly IApplicationRuntimeHost _runtimeHost;
    private readonly Func<Task> _toggleWindowAsync;
    private readonly Func<Task> _openSettingsAsync;
    private readonly Func<Task> _exitAsync;
    private readonly object _sync = new();

    private bool _initialized;
    private bool _shutdownStarted;
    private ProviderRefreshStatus _refreshStatus;
    private bool _refreshInProgress;
    private bool _disposed;

    public NotificationAreaController(
        INotificationAreaIcon icon,
        IApplicationRuntimeHost runtimeHost,
        Func<Task> toggleWindowAsync,
        Func<Task> openSettingsAsync,
        Func<Task> exitAsync)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(runtimeHost);
        ArgumentNullException.ThrowIfNull(toggleWindowAsync);
        ArgumentNullException.ThrowIfNull(openSettingsAsync);
        ArgumentNullException.ThrowIfNull(exitAsync);

        _icon = icon;
        _runtimeHost = runtimeHost;
        _toggleWindowAsync = toggleWindowAsync;
        _openSettingsAsync = openSettingsAsync;
        _exitAsync = exitAsync;
        _refreshStatus = runtimeHost.RefreshStatus;

        _icon.CommandRequested += OnCommandRequested;
        _icon.ShellFaulted += OnShellFaulted;
        _runtimeHost.RefreshStatusChanged += OnRefreshStatusChanged;
        UpdateRefreshCommandAvailability();
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

        _icon.Initialize();
    }

    public void RestoreIcon()
    {
        lock (_sync)
        {
            if (_disposed || !_initialized)
            {
                return;
            }
        }

        _icon.Restore();
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
            _shutdownStarted = true;
        }

        _icon.CommandRequested -= OnCommandRequested;
        _icon.ShellFaulted -= OnShellFaulted;
        _runtimeHost.RefreshStatusChanged -= OnRefreshStatusChanged;
        _icon.Dispose();
    }

    private void OnCommandRequested(NotificationAreaCommand command)
    {
        _ = DispatchCommandAsync(command);
    }

    private async Task DispatchCommandAsync(NotificationAreaCommand command)
    {
        try
        {
            switch (command)
            {
                case NotificationAreaCommand.PrimaryActivate:
                    if (CommandsAreClosed())
                    {
                        return;
                    }

                    await _toggleWindowAsync().ConfigureAwait(false);
                    break;
                case NotificationAreaCommand.Refresh:
                    await RefreshAsync().ConfigureAwait(false);
                    break;
                case NotificationAreaCommand.Settings:
                    if (CommandsAreClosed())
                    {
                        return;
                    }

                    await _openSettingsAsync().ConfigureAwait(false);
                    break;
                case NotificationAreaCommand.Exit:
                    await ExitAsync().ConfigureAwait(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }
        catch
        {
            if (!CommandsAreClosed())
            {
                _runtimeHost.ReportShellFault();
            }
        }
    }

    private async Task RefreshAsync()
    {
        lock (_sync)
        {
            if (_disposed ||
                _shutdownStarted ||
                _refreshInProgress ||
                _refreshStatus.IsRefreshActive)
            {
                return;
            }

            _refreshInProgress = true;
        }

        UpdateRefreshCommandAvailability();

        try
        {
            await _runtimeHost.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _refreshInProgress = false;
            }

            UpdateRefreshCommandAvailability();
        }
    }

    private async Task ExitAsync()
    {
        lock (_sync)
        {
            if (_disposed || _shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
        }

        await _exitAsync().ConfigureAwait(false);
    }

    private bool CommandsAreClosed()
    {
        lock (_sync)
        {
            return _disposed || _shutdownStarted;
        }
    }

    private void OnShellFaulted()
    {
        if (!CommandsAreClosed())
        {
            _runtimeHost.ReportShellFault();
        }
    }

    private void OnRefreshStatusChanged(ProviderRefreshStatus status)
    {
        lock (_sync)
        {
            _refreshStatus = status;
        }

        UpdateRefreshCommandAvailability();
    }

    private void UpdateRefreshCommandAvailability()
    {
        NotificationAreaRefreshCommandState state;
        lock (_sync)
        {
            if (_disposed || _shutdownStarted)
            {
                return;
            }

            state = _refreshInProgress || _refreshStatus.IsRefreshActive
                ? NotificationAreaRefreshCommandState.Updating
                : NotificationAreaRefreshCommandState.Available;
        }

        _icon.SetRefreshCommandState(state);
    }
}
