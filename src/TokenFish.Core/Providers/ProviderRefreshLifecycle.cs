using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public sealed class ProviderRefreshLifecycle : IProviderRefreshLifecycle
{
    private static readonly Func<TimeSpan, TimeProvider, CancellationToken, Task> DefaultDelayAsync =
        static (delay, timeProvider, cancellationToken) =>
            Task.Delay(delay, timeProvider, cancellationToken);

    private readonly ProviderUsageCollectionCoordinator _coordinator;
    private readonly AppSettings _settings;
    private readonly TimeSpan _refreshInterval;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan, TimeProvider, CancellationToken, Task> _delayAsync;
    private readonly IProviderRuntimeSnapshotStore? _snapshotStore;
    private readonly CancellationTokenSource _shutdownCancellationTokenSource = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _sync = new();

    private CancellationTokenSource? _scheduleCancellationTokenSource;
    private Task? _scheduleTask;
    private Task? _initialRefreshTask;
    private bool _started;
    private bool _stopped;
    private bool _disposed;

    public ProviderRefreshLifecycle(
        ProviderUsageCollectionCoordinator coordinator,
        AppSettings settings,
        TimeSpan refreshInterval,
        TimeProvider? timeProvider = null,
        IProviderRuntimeSnapshotStore? snapshotStore = null)
        : this(
            coordinator,
            settings,
            refreshInterval,
            timeProvider ?? TimeProvider.System,
            DefaultDelayAsync,
            snapshotStore)
    {
    }

    internal ProviderRefreshLifecycle(
        ProviderUsageCollectionCoordinator coordinator,
        AppSettings settings,
        TimeSpan refreshInterval,
        TimeProvider timeProvider,
        Func<TimeSpan, TimeProvider, CancellationToken, Task> delayAsync,
        IProviderRuntimeSnapshotStore? snapshotStore = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(delayAsync);

        if (refreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshInterval),
                "Refresh interval must be greater than zero.");
        }

        _coordinator = coordinator;
        _settings = settings;
        _refreshInterval = refreshInterval;
        _timeProvider = timeProvider;
        _delayAsync = delayAsync;
        _snapshotStore = snapshotStore;
    }

    public Task Completion
    {
        get
        {
            lock (_sync)
            {
                return _scheduleTask ?? Task.CompletedTask;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task initialRefreshTask;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_stopped)
            {
                throw new InvalidOperationException(
                    "Provider refresh lifecycle cannot be restarted after it has stopped.");
            }

            if (!_started)
            {
                _started = true;
                _scheduleCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    _shutdownCancellationTokenSource.Token);
                var lifetimeToken = _scheduleCancellationTokenSource.Token;
                var initialRefreshCompletion =
                    new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                _initialRefreshTask = initialRefreshCompletion.Task;
                _scheduleTask = RunScheduleAsync(lifetimeToken, initialRefreshCompletion);
            }

            initialRefreshTask = _initialRefreshTask!;
        }

        return initialRefreshTask.WaitAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ProviderUsageSnapshot>> RefreshAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            if (_stopped)
            {
                throw new InvalidOperationException(
                    "Provider refresh lifecycle has stopped.");
            }
        }

        return RefreshWithShutdownAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? scheduleTask;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (!_stopped)
            {
                _stopped = true;
                _shutdownCancellationTokenSource.Cancel();
                _scheduleCancellationTokenSource?.Cancel();
            }

            scheduleTask = _scheduleTask;
        }

        if (scheduleTask is not null)
        {
            await scheduleTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _refreshGate.Release();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _scheduleCancellationTokenSource?.Dispose();
        _shutdownCancellationTokenSource.Dispose();
        _refreshGate.Dispose();
    }

    private async Task RunScheduleAsync(
        CancellationToken lifetimeToken,
        TaskCompletionSource initialRefreshCompletion)
    {
        try
        {
            await RunRefreshAsync(lifetimeToken).ConfigureAwait(false);
            initialRefreshCompletion.TrySetResult();

            while (true)
            {
                await _delayAsync(_refreshInterval, _timeProvider, lifetimeToken)
                    .ConfigureAwait(false);
                await RunRefreshAsync(lifetimeToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exception)
            when (lifetimeToken.IsCancellationRequested)
        {
            initialRefreshCompletion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            initialRefreshCompletion.TrySetException(exception);
            throw;
        }
    }

    private async Task<IReadOnlyList<ProviderUsageSnapshot>> RefreshWithShutdownAsync(
        CancellationToken cancellationToken)
    {
        using var refreshCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _shutdownCancellationTokenSource.Token);

        return await RunRefreshAsync(refreshCancellationTokenSource.Token)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ProviderUsageSnapshot>> RunRefreshAsync(
        CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshots = await _coordinator.CollectAsync(_settings, cancellationToken)
                .ConfigureAwait(false);
            _snapshotStore?.Store(snapshots);

            return snapshots;
        }
        finally
        {
            _refreshGate.Release();
        }
    }
}
