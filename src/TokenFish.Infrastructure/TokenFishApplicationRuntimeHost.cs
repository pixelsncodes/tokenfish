using TokenFish.Core.Models;
using TokenFish.Core.Settings;

namespace TokenFish.Infrastructure;

public sealed class TokenFishApplicationRuntimeHost : IAsyncDisposable
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly string _clientVersion;
    private readonly Func<AppSettings, string, IApplicationRuntimeServices> _createServices;
    private readonly object _sync = new();
    private readonly CancellationTokenSource _shutdownCancellationTokenSource = new();

    private IApplicationRuntimeServices? _services;
    private Task? _startupTask;
    private Task? _lifecycleCompletionObserver;
    private Task? _shutdownTask;
    private ApplicationRuntimeStatus _status = ApplicationRuntimeStatus.Stopped;
    private bool _shutdownStarted;

    public TokenFishApplicationRuntimeHost(
        IAppSettingsStore settingsStore,
        string clientVersion)
        : this(settingsStore, clientVersion, TokenFishApplicationServices.Create)
    {
    }

    internal TokenFishApplicationRuntimeHost(
        IAppSettingsStore settingsStore,
        string clientVersion,
        Func<AppSettings, string, IApplicationRuntimeServices> createServices)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
        ArgumentNullException.ThrowIfNull(createServices);

        _settingsStore = settingsStore;
        _clientVersion = clientVersion;
        _createServices = createServices;
    }

    public event Action<ApplicationRuntimeStatus>? StatusChanged;

    public ApplicationRuntimeStatus Status
    {
        get
        {
            lock (_sync)
            {
                return _status;
            }
        }
    }

    public IApplicationRuntimeServices? Services
    {
        get
        {
            lock (_sync)
            {
                return _services;
            }
        }
    }

    /// <summary>
    /// Starts the runtime once. Repeated calls while startup is active or running return the
    /// original startup task and never construct duplicate services.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_startupTask is null)
            {
                SetStatusUnderLock(ApplicationRuntimeStatus.Starting);
                _startupTask = StartCoreAsync();
            }

            return cancellationToken.CanBeCanceled
                ? _startupTask.WaitAsync(cancellationToken)
                : _startupTask;
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IApplicationRuntimeServices? services;

        lock (_sync)
        {
            if (_shutdownStarted ||
                _status.State is ApplicationRuntimeState.Starting or
                    ApplicationRuntimeState.Stopping or
                    ApplicationRuntimeState.Stopped)
            {
                return;
            }

            services = _services;
            if (services is null)
            {
                return;
            }

            SetStatusUnderLock(ApplicationRuntimeStatus.Refreshing);
        }

        try
        {
            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _shutdownCancellationTokenSource.Token);
            await services.ProviderRefreshLifecycle
                .RefreshAsync(linkedCancellation.Token)
                .ConfigureAwait(false);

            lock (_sync)
            {
                if (!_shutdownStarted && _status.Issue != ApplicationRuntimeIssue.StartupFailed)
                {
                    SetStatusUnderLock(ApplicationRuntimeStatus.Running);
                }
            }
        }
        catch (OperationCanceledException)
            when (_shutdownCancellationTokenSource.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            lock (_sync)
            {
                if (!_shutdownStarted)
                {
                    SetStatusUnderLock(ApplicationRuntimeStatus.RefreshFaulted);
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _shutdownTask ??= StopCoreAsync();

            return cancellationToken.CanBeCanceled
                ? _shutdownTask.WaitAsync(cancellationToken)
                : _shutdownTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _shutdownCancellationTokenSource.Dispose();
    }

    private async Task StartCoreAsync()
    {
        try
        {
            var settings = await _settingsStore
                .LoadAsync(_shutdownCancellationTokenSource.Token)
                .ConfigureAwait(false);

            _shutdownCancellationTokenSource.Token.ThrowIfCancellationRequested();

            var services = _createServices(settings, _clientVersion);

            lock (_sync)
            {
                _services = services;
            }

            var lifecycle = services.ProviderRefreshLifecycle;
            var startTask = lifecycle.StartAsync(_shutdownCancellationTokenSource.Token);
            var completion = lifecycle.Completion;
            _lifecycleCompletionObserver = ObserveLifecycleCompletionAsync(completion);

            await startTask.ConfigureAwait(false);

            lock (_sync)
            {
                if (!_shutdownStarted && _status.State != ApplicationRuntimeState.Faulted)
                {
                    SetStatusUnderLock(ApplicationRuntimeStatus.Running);
                }
            }
        }
        catch (OperationCanceledException)
            when (_shutdownCancellationTokenSource.IsCancellationRequested)
        {
            lock (_sync)
            {
                if (_services is null)
                {
                    SetStatusUnderLock(ApplicationRuntimeStatus.Stopped);
                }
            }
        }
        catch
        {
            lock (_sync)
            {
                if (!_shutdownStarted)
                {
                    SetStatusUnderLock(ApplicationRuntimeStatus.StartupFaulted);
                }
            }
        }
    }

    private async Task ObserveLifecycleCompletionAsync(Task completion)
    {
        try
        {
            await completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (_shutdownCancellationTokenSource.IsCancellationRequested)
        {
        }
        catch
        {
            lock (_sync)
            {
                if (!_shutdownStarted)
                {
                    SetStatusUnderLock(ApplicationRuntimeStatus.RefreshFaulted);
                }
            }
        }
    }

    private async Task StopCoreAsync()
    {
        IApplicationRuntimeServices? services;
        Task? startupTask;
        Task? completionObserver;

        lock (_sync)
        {
            _shutdownStarted = true;
            _shutdownCancellationTokenSource.Cancel();

            if (_status.State != ApplicationRuntimeState.Stopped)
            {
                SetStatusUnderLock(ApplicationRuntimeStatus.Stopping);
            }

            services = _services;
            startupTask = _startupTask;
            completionObserver = _lifecycleCompletionObserver;
        }

        try
        {
            if (services is not null)
            {
                await services.ProviderRefreshLifecycle.StopAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (startupTask is not null)
            {
                await SuppressOwnedFaultAsync(startupTask).ConfigureAwait(false);
            }

            if (completionObserver is not null)
            {
                await SuppressOwnedFaultAsync(completionObserver).ConfigureAwait(false);
            }

            if (services is not null)
            {
                await services.DisposeAsync().ConfigureAwait(false);
            }

            lock (_sync)
            {
                SetStatusUnderLock(ApplicationRuntimeStatus.Stopped);
            }
        }
        catch
        {
            lock (_sync)
            {
                SetStatusUnderLock(ApplicationRuntimeStatus.ShutdownFaulted);
            }
        }
    }

    private static async Task SuppressOwnedFaultAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private void SetStatusUnderLock(ApplicationRuntimeStatus status)
    {
        if (_status == status)
        {
            return;
        }

        _status = status;
        StatusChanged?.Invoke(status);
    }
}
