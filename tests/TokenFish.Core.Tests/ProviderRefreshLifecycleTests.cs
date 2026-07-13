using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Core.Tests;

public sealed class ProviderRefreshLifecycleTests
{
    [Fact]
    public async Task InitialRefreshStatusIsNeverRefreshed()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);

        await using var lifecycle = CreateLifecycle(collector, timeProvider: timeProvider);

        Assert.False(lifecycle.RefreshStatus.IsRefreshActive);
        Assert.Null(lifecycle.RefreshStatus.LatestAttemptedAtUtc);
        Assert.Null(lifecycle.RefreshStatus.LatestSucceededAtUtc);
        Assert.Equal(ProviderRefreshOutcome.NeverRefreshed, lifecycle.RefreshStatus.Outcome);
        Assert.Equal(0, lifecycle.RefreshStatus.Version);
    }

    [Fact]
    public void ConstructionPerformsNoRefresh()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);

        _ = CreateLifecycle(collector);

        Assert.Equal(0, collector.CallCount);
    }

    [Fact]
    public async Task StartPerformsOneInitialRefresh()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(collector);

        await lifecycle.StartAsync(CancellationToken.None);

        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task SuccessfulStartupRefreshUpdatesStatusWithInjectedTime()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(collector, timeProvider: timeProvider);
        var statuses = new List<ProviderRefreshStatus>();
        lifecycle.RefreshStatusChanged += statuses.Add;

        await lifecycle.StartAsync(CancellationToken.None);

        Assert.Equal(2, statuses.Count);
        Assert.True(statuses[0].IsRefreshActive);
        Assert.Equal(ProviderRefreshOutcome.Refreshing, statuses[0].Outcome);
        Assert.Equal(timeProvider.InitialUtcNow, statuses[0].LatestAttemptedAtUtc);
        Assert.False(lifecycle.RefreshStatus.IsRefreshActive);
        Assert.Equal(ProviderRefreshOutcome.Succeeded, lifecycle.RefreshStatus.Outcome);
        Assert.Equal(timeProvider.InitialUtcNow, lifecycle.RefreshStatus.LatestSucceededAtUtc);
        Assert.Equal(2, lifecycle.RefreshStatus.Version);
    }

    [Fact]
    public async Task ScheduledRefreshOccursUsingDeterministicDelay()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var delay = new ManualRefreshDelay();
        await using var lifecycle = CreateLifecycle(collector, delay: delay);

        await lifecycle.StartAsync(CancellationToken.None);
        await delay.CompleteNextAsync();
        await collector.WaitForCallCountAsync(2);

        Assert.Equal(2, collector.CallCount);
    }

    [Fact]
    public async Task SuccessfulScheduledRefreshUpdatesAttemptAndSuccessTimes()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var delay = new ManualRefreshDelay();
        await using var lifecycle = CreateLifecycle(
            collector,
            delay: delay,
            timeProvider: timeProvider);

        await lifecycle.StartAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await delay.CompleteNextAsync();
        await collector.WaitForCallCountAsync(2);

        Assert.Equal(
            new DateTimeOffset(2026, 7, 12, 8, 5, 0, TimeSpan.Zero),
            lifecycle.RefreshStatus.LatestAttemptedAtUtc);
        Assert.Equal(
            new DateTimeOffset(2026, 7, 12, 8, 5, 0, TimeSpan.Zero),
            lifecycle.RefreshStatus.LatestSucceededAtUtc);
        Assert.Equal(ProviderRefreshOutcome.Succeeded, lifecycle.RefreshStatus.Outcome);
        Assert.Equal(4, lifecycle.RefreshStatus.Version);
    }

    [Fact]
    public async Task TwoRefreshesNeverOverlap()
    {
        var collector = new BlockingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(collector);

        var firstRefresh = lifecycle.RefreshAsync(CancellationToken.None);
        await collector.WaitForCallCountAsync(1);
        var secondRefresh = lifecycle.RefreshAsync(CancellationToken.None);

        Assert.Equal(1, collector.CallCount);
        Assert.False(secondRefresh.IsCompleted);

        collector.CompleteNext();
        await firstRefresh;
        await collector.WaitForCallCountAsync(2);
        collector.CompleteNext();
        await secondRefresh;

        Assert.Equal(1, collector.MaxConcurrentCalls);
        Assert.Equal(ProviderRefreshOutcome.Succeeded, lifecycle.RefreshStatus.Outcome);
    }

    [Fact]
    public async Task ManualRefreshWorks()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(collector);

        var snapshots = await lifecycle.RefreshAsync(CancellationToken.None);

        Assert.Single(snapshots);
        Assert.Equal(ProviderKind.Codex, snapshots[0].Provider);
        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task SuccessfulManualRefreshUpdatesStatus()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero));
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(collector, timeProvider: timeProvider);

        await lifecycle.RefreshAsync(CancellationToken.None);

        Assert.False(lifecycle.RefreshStatus.IsRefreshActive);
        Assert.Equal(ProviderRefreshOutcome.Succeeded, lifecycle.RefreshStatus.Outcome);
        Assert.Equal(timeProvider.InitialUtcNow, lifecycle.RefreshStatus.LatestAttemptedAtUtc);
        Assert.Equal(timeProvider.InitialUtcNow, lifecycle.RefreshStatus.LatestSucceededAtUtc);
    }

    [Fact]
    public async Task ExpectedRefreshFailureRecordsSafeFailedStatus()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero));
        var collector = new RecordingProviderUsageCollector(
            ProviderKind.Codex,
            _ => Task.FromException<ProviderUsageSnapshot>(
                new InvalidOperationException("synthetic secret C:\\Users\\pixel")));
        await using var lifecycle = CreateLifecycle(collector, timeProvider: timeProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycle.RefreshAsync(CancellationToken.None));

        Assert.False(lifecycle.RefreshStatus.IsRefreshActive);
        Assert.Equal(ProviderRefreshOutcome.Failed, lifecycle.RefreshStatus.Outcome);
        Assert.Equal(timeProvider.InitialUtcNow, lifecycle.RefreshStatus.LatestAttemptedAtUtc);
        Assert.Null(lifecycle.RefreshStatus.LatestSucceededAtUtc);
        Assert.DoesNotContain("secret", lifecycle.RefreshStatus.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Users", lifecycle.RefreshStatus.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShutdownCancellationDoesNotBecomeUserVisibleFailure()
    {
        var collector = new BlockingProviderUsageCollector(ProviderKind.Codex)
        {
            CompleteImmediately = true
        };
        await using var lifecycle = CreateLifecycle(collector);

        await lifecycle.StartAsync(CancellationToken.None);
        collector.CompleteImmediately = false;
        var manualRefresh = lifecycle.RefreshAsync(CancellationToken.None);
        await collector.WaitForCallCountAsync(2);

        await lifecycle.StopAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await manualRefresh);

        Assert.False(lifecycle.RefreshStatus.IsRefreshActive);
        Assert.Equal(ProviderRefreshOutcome.Succeeded, lifecycle.RefreshStatus.Outcome);
        Assert.NotNull(lifecycle.RefreshStatus.LatestSucceededAtUtc);
    }

    [Fact]
    public async Task LastSuccessTimeSurvivesLaterFailure()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 11, 0, 0, TimeSpan.Zero));
        var collector = new SequencedProviderUsageCollector(ProviderKind.Codex);
        collector.Enqueue(_ => Task.FromResult(CreateSnapshot(ProviderKind.Codex)));
        collector.Enqueue(_ => Task.FromException<ProviderUsageSnapshot>(
            new InvalidOperationException("synthetic failure")));
        await using var lifecycle = CreateLifecycle(collector, timeProvider: timeProvider);

        await lifecycle.RefreshAsync(CancellationToken.None);
        var succeededAt = lifecycle.RefreshStatus.LatestSucceededAtUtc;
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycle.RefreshAsync(CancellationToken.None));

        Assert.Equal(ProviderRefreshOutcome.Failed, lifecycle.RefreshStatus.Outcome);
        Assert.Equal(succeededAt, lifecycle.RefreshStatus.LatestSucceededAtUtc);
        Assert.Equal(timeProvider.GetUtcNow(), lifecycle.RefreshStatus.LatestAttemptedAtUtc);
    }

    [Fact]
    public async Task SnapshotStoreKeepsLastSuccessfulSnapshotDuringInProgressAndFailedRefresh()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero));
        var snapshotStore = new InMemoryProviderRuntimeSnapshotStore(
            timeProvider,
            TimeSpan.FromMinutes(5));
        var collector = new BlockingProviderUsageCollector(ProviderKind.Codex)
        {
            CompleteImmediately = true
        };
        await using var lifecycle = CreateLifecycle(
            collector,
            timeProvider: timeProvider,
            snapshotStore: snapshotStore);

        await lifecycle.RefreshAsync(CancellationToken.None);
        Assert.True(snapshotStore.TryGetCurrent(ProviderKind.Codex, out var firstState));

        collector.CompleteImmediately = false;
        collector.FailNextOnComplete = true;
        var failedRefresh = lifecycle.RefreshAsync(CancellationToken.None);
        await collector.WaitForCallCountAsync(2);

        Assert.True(lifecycle.RefreshStatus.IsRefreshActive);
        Assert.True(snapshotStore.TryGetCurrent(ProviderKind.Codex, out var inProgressState));
        Assert.Equal(firstState.Snapshot, inProgressState.Snapshot);

        collector.CompleteNext();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failedRefresh);

        Assert.True(snapshotStore.TryGetCurrent(ProviderKind.Codex, out var failedState));
        Assert.Equal(firstState.Snapshot, failedState.Snapshot);
        Assert.Equal(ProviderRefreshOutcome.Failed, lifecycle.RefreshStatus.Outcome);
    }

    [Fact]
    public async Task ManualAndScheduledRefreshesSerializeThroughOneGate()
    {
        var collector = new BlockingProviderUsageCollector(ProviderKind.Codex)
        {
            CompleteImmediately = true
        };
        var delay = new ManualRefreshDelay();
        await using var lifecycle = CreateLifecycle(collector, delay: delay);

        await lifecycle.StartAsync(CancellationToken.None);
        collector.CompleteImmediately = false;
        var manualRefresh = lifecycle.RefreshAsync(CancellationToken.None);
        await collector.WaitForCallCountAsync(2);
        await delay.CompleteNextAsync();

        Assert.Equal(2, collector.CallCount);

        collector.CompleteNext();
        await manualRefresh;
        await collector.WaitForCallCountAsync(3);
        collector.CompleteNext();

        Assert.Equal(1, collector.MaxConcurrentCalls);
    }

    [Fact]
    public async Task DisabledProviderIsNeverCalled()
    {
        var claudeCollector = new RecordingProviderUsageCollector(ProviderKind.Claude);
        var codexCollector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(
            [claudeCollector, codexCollector],
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly });

        await lifecycle.StartAsync(CancellationToken.None);

        Assert.Equal(0, claudeCollector.CallCount);
        Assert.Equal(1, codexCollector.CallCount);
    }

    [Fact]
    public async Task StopCancelsSchedule()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var delay = new ManualRefreshDelay();
        await using var lifecycle = CreateLifecycle(collector, delay: delay);

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);
        await delay.CompleteNextAsync();

        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task StopDuringActiveRefreshCancelsRefreshAndWaitsForGate()
    {
        var collector = new BlockingProviderUsageCollector(ProviderKind.Codex)
        {
            CompleteImmediately = true
        };
        await using var lifecycle = CreateLifecycle(collector);

        await lifecycle.StartAsync(CancellationToken.None);
        collector.CompleteImmediately = false;
        var manualRefresh = lifecycle.RefreshAsync(CancellationToken.None);
        await collector.WaitForCallCountAsync(2);

        await lifecycle.StopAsync(CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await manualRefresh);
        Assert.True(collector.LastCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task NoRefreshBeginsAfterStopCompletes()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(collector);

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycle.RefreshAsync(CancellationToken.None));
        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task RepeatedStopAndDisposalAreSafe()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var lifecycle = CreateLifecycle(collector);

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);
        await lifecycle.DisposeAsync();
        await lifecycle.DisposeAsync();

        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task RepeatedStartIsIdempotentWhileRunning()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(collector);

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StartAsync(CancellationToken.None);

        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task StartAfterStopIsRejected()
    {
        var collector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        await using var lifecycle = CreateLifecycle(collector);

        await lifecycle.StartAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycle.StartAsync(CancellationToken.None));

        Assert.Contains("cannot be restarted", exception.Message);
    }

    [Fact]
    public async Task CancellationPropagatesToCoordinator()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var collector = new RecordingProviderUsageCollector(
            ProviderKind.Codex,
            cancellationToken =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromCanceled<ProviderUsageSnapshot>(cancellationToken);
            });
        await using var lifecycle = CreateLifecycle(collector);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            lifecycle.RefreshAsync(cancellationTokenSource.Token));

        Assert.True(collector.LastCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task UnexpectedExceptionsAreSurfacedThroughStartAndCompletion()
    {
        var expectedException = new InvalidOperationException("synthetic collector failure");
        var collector = new RecordingProviderUsageCollector(
            ProviderKind.Codex,
            _ => Task.FromException<ProviderUsageSnapshot>(expectedException));
        var lifecycle = CreateLifecycle(collector);

        var startException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycle.StartAsync(CancellationToken.None));
        var completionException = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await lifecycle.Completion);

        Assert.Same(expectedException, startException);
        Assert.Same(expectedException, completionException);
    }

    [Fact]
    public async Task NoAutomaticRetryOccursAfterFailure()
    {
        var collector = new RecordingProviderUsageCollector(
            ProviderKind.Codex,
            _ => Task.FromException<ProviderUsageSnapshot>(
                new InvalidOperationException("synthetic collector failure")));
        var delay = new ManualRefreshDelay();
        var lifecycle = CreateLifecycle(collector, delay: delay);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycle.StartAsync(CancellationToken.None));

        Assert.Equal(1, collector.CallCount);
        Assert.Equal(0, delay.DelayCallCount);
    }

    private static ProviderRefreshLifecycle CreateLifecycle(
        IProviderUsageCollector collector,
        AppSettings? settings = null,
        ManualRefreshDelay? delay = null,
        TimeProvider? timeProvider = null,
        IProviderRuntimeSnapshotStore? snapshotStore = null) =>
        CreateLifecycle([collector], settings, delay, timeProvider, snapshotStore);

    private static ProviderRefreshLifecycle CreateLifecycle(
        IEnumerable<IProviderUsageCollector> collectors,
        AppSettings? settings = null,
        ManualRefreshDelay? delay = null,
        TimeProvider? timeProvider = null,
        IProviderRuntimeSnapshotStore? snapshotStore = null)
    {
        var refreshDelay = delay ?? new ManualRefreshDelay();

        return new ProviderRefreshLifecycle(
            new ProviderUsageCollectionCoordinator(collectors),
            settings ?? new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            TimeSpan.FromMinutes(5),
            timeProvider ?? TimeProvider.System,
            refreshDelay.DelayAsync,
            snapshotStore);
    }

    private sealed class SequencedProviderUsageCollector : IProviderUsageCollector
    {
        private readonly Queue<Func<CancellationToken, Task<ProviderUsageSnapshot>>> _callbacks = new();

        public SequencedProviderUsageCollector(ProviderKind provider)
        {
            Provider = provider;
        }

        public ProviderKind Provider { get; }

        public void Enqueue(Func<CancellationToken, Task<ProviderUsageSnapshot>> callback) =>
            _callbacks.Enqueue(callback);

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken) =>
            _callbacks.Dequeue()(cancellationToken);
    }

    private sealed class RecordingProviderUsageCollector : IProviderUsageCollector
    {
        private readonly Func<CancellationToken, Task<ProviderUsageSnapshot>> _collectAsync;
        private readonly SemaphoreSlim _callSignal = new(0);

        public RecordingProviderUsageCollector(
            ProviderKind provider,
            Func<CancellationToken, Task<ProviderUsageSnapshot>>? collectAsync = null)
        {
            Provider = provider;
            _collectAsync = collectAsync ?? (_ => Task.FromResult(CreateSnapshot(provider)));
        }

        public ProviderKind Provider { get; }

        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            LastCancellationToken = cancellationToken;
            _callSignal.Release();

            return await _collectAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task WaitForCallCountAsync(int expectedCallCount)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            while (CallCount < expectedCallCount)
            {
                await _callSignal.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
        }
    }

    private sealed class BlockingProviderUsageCollector : IProviderUsageCollector
    {
        private readonly Queue<TaskCompletionSource> _pendingCompletions = new();
        private readonly SemaphoreSlim _callSignal = new(0);
        private int _activeCalls;

        public BlockingProviderUsageCollector(ProviderKind provider)
        {
            Provider = provider;
        }

        public ProviderKind Provider { get; }

        public bool CompleteImmediately { get; set; }

        public bool FailNextOnComplete { get; set; }

        public int CallCount { get; private set; }

        public int MaxConcurrentCalls { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            var activeCalls = Interlocked.Increment(ref _activeCalls);
            MaxConcurrentCalls = Math.Max(MaxConcurrentCalls, activeCalls);
            CallCount++;
            LastCancellationToken = cancellationToken;
            _callSignal.Release();

            try
            {
                if (!CompleteImmediately)
                {
                    var completion = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                    lock (_pendingCompletions)
                    {
                        _pendingCompletions.Enqueue(completion);
                    }

                    await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                if (FailNextOnComplete)
                {
                    FailNextOnComplete = false;
                    throw new InvalidOperationException("synthetic refresh failure");
                }

                return CreateSnapshot(Provider);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCalls);
            }
        }

        public void CompleteNext()
        {
            TaskCompletionSource completion;

            lock (_pendingCompletions)
            {
                completion = _pendingCompletions.Dequeue();
            }

            completion.SetResult();
        }

        public async Task WaitForCallCountAsync(int expectedCallCount)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            while (CallCount < expectedCallCount)
            {
                await _callSignal.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
        }
    }

    private sealed class ManualRefreshDelay
    {
        private readonly Queue<TaskCompletionSource> _pendingDelays = new();
        private readonly SemaphoreSlim _delaySignal = new(0);

        public int DelayCallCount { get; private set; }

        public Task DelayAsync(
            TimeSpan _,
            TimeProvider __,
            CancellationToken cancellationToken)
        {
            DelayCallCount++;
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

            lock (_pendingDelays)
            {
                _pendingDelays.Enqueue(completion);
            }

            _delaySignal.Release();

            return completion.Task;
        }

        public async Task CompleteNextAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _delaySignal.WaitAsync(timeout.Token).ConfigureAwait(false);

            TaskCompletionSource completion;

            lock (_pendingDelays)
            {
                completion = _pendingDelays.Dequeue();
            }

            completion.TrySetResult();
        }
    }

    private static ProviderUsageSnapshot CreateSnapshot(ProviderKind provider) =>
        new(
            provider,
            ProviderConnectionState.Connected,
            new PercentageUsageMetric(
                50m,
                DataAuthority.LocalProviderReported,
                DataFreshness.Live),
            DateTimeOffset.UtcNow.AddHours(1),
            new TokenCountMetric(
                100,
                DataAuthority.LocalProviderReported,
                DataFreshness.Live),
            new TokenCountMetric(
                1_000,
                DataAuthority.TokenFishDerived,
                DataFreshness.Cached),
            DateTimeOffset.UtcNow);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            InitialUtcNow = utcNow.ToUniversalTime();
            _utcNow = InitialUtcNow;
        }

        public DateTimeOffset InitialUtcNow { get; }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
    }
}
