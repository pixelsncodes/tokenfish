using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Core.Tests;

public sealed class InMemoryProviderRuntimeSnapshotStoreTests
{
    [Fact]
    public void EmptyStoreHasNoProviderState()
    {
        var store = CreateStore();

        Assert.False(store.TryGetCurrent(ProviderKind.Codex, out _));
        Assert.Empty(store.GetCurrentSnapshots());
    }

    [Fact]
    public void FirstSnapshotIsStoredWithUtcCaptureTime()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var store = CreateStore(timeProvider);
        var snapshot = CreateSnapshot(ProviderKind.Codex);

        store.Store([snapshot]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Same(snapshot, state.Snapshot);
        Assert.Equal(timeProvider.GetUtcNow(), state.AcceptedAt);
        Assert.Equal(TimeSpan.Zero, state.AcceptedAt.Offset);
    }

    [Fact]
    public void CollectionOutcomeIsStoredWithSnapshotWithoutChangingSourceObservationTime()
    {
        var store = CreateStore();
        var observedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var snapshot = CreateSnapshot(ProviderKind.Claude, sourceObservedAt: observedAt);

        store.Store([
            new ProviderCollectionResult(
                snapshot,
                ProviderCollectionOutcome.Failed,
                ProviderCollectionFailureReason.ClaudeBridgeMalformed)
        ]);

        Assert.True(store.TryGetCurrent(ProviderKind.Claude, out var state));
        Assert.Same(snapshot, state.Snapshot);
        Assert.Equal(observedAt, state.Snapshot.SourceObservedAt);
        Assert.Equal(ProviderCollectionOutcome.Failed, state.CollectionOutcome);
        Assert.Equal(ProviderCollectionFailureReason.ClaudeBridgeMalformed, state.CollectionFailureReason);
    }

    [Fact]
    public async Task RefreshLifecycleCommitsSnapshotAndCollectionOutcomeTogether()
    {
        var store = CreateStore();
        var snapshot = CreateUnavailableSnapshot(ProviderKind.Codex);
        var collector = new OutcomeProviderUsageCollector(
            new ProviderCollectionResult(
                snapshot,
                ProviderCollectionOutcome.Failed,
                ProviderCollectionFailureReason.CodexProtocol));
        await using var lifecycle = CreateLifecycle(collector, store);

        await lifecycle.RefreshAsync(CancellationToken.None);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Same(snapshot, state.Snapshot);
        Assert.Equal(ProviderCollectionOutcome.Failed, state.CollectionOutcome);
        Assert.Equal(ProviderCollectionFailureReason.CodexProtocol, state.CollectionFailureReason);
    }

    [Fact]
    public void NewSnapshotReplacesPriorSnapshotForSameProvider()
    {
        var store = CreateStore();
        var first = CreateSnapshot(ProviderKind.Codex, sessionTokens: 100);
        var second = CreateSnapshot(ProviderKind.Codex, sessionTokens: 200);

        store.Store([first]);
        store.Store([second]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Same(second, state.Snapshot);
        Assert.Equal(200, state.Snapshot.SessionTokens.TokenCount);
        Assert.Single(store.GetCurrentSnapshots());
    }

    [Fact]
    public void UpdatingCodexDoesNotOverwriteClaudeState()
    {
        var store = CreateStore();
        var claude = CreateSnapshot(ProviderKind.Claude, sessionTokens: 10);
        var codex = CreateSnapshot(ProviderKind.Codex, sessionTokens: 20);

        store.Store([claude]);
        store.Store([codex]);

        Assert.True(store.TryGetCurrent(ProviderKind.Claude, out var claudeState));
        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var codexState));
        Assert.Equal(10, claudeState.Snapshot.SessionTokens.TokenCount);
        Assert.Equal(20, codexState.Snapshot.SessionTokens.TokenCount);
    }

    [Fact]
    public void UpdatingClaudeDoesNotOverwriteCodexState()
    {
        var store = CreateStore();
        var codex = CreateSnapshot(ProviderKind.Codex, sessionTokens: 20);
        var claude = CreateSnapshot(ProviderKind.Claude, sessionTokens: 10);

        store.Store([codex]);
        store.Store([claude]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var codexState));
        Assert.True(store.TryGetCurrent(ProviderKind.Claude, out var claudeState));
        Assert.Equal(20, codexState.Snapshot.SessionTokens.TokenCount);
        Assert.Equal(10, claudeState.Snapshot.SessionTokens.TokenCount);
    }

    [Fact]
    public void ConcurrentReadsAndWritesAreSafe()
    {
        var store = CreateStore();

        Parallel.For(
            0,
            1_000,
            index =>
            {
                var provider = index % 2 == 0 ? ProviderKind.Codex : ProviderKind.Claude;
                store.Store([CreateSnapshot(provider, sessionTokens: index)]);
                _ = store.TryGetCurrent(provider, out _);
                _ = store.GetCurrentSnapshots();
            });

        Assert.True(store.GetCurrentSnapshots().Count <= 2);
    }

    [Fact]
    public void ReturnedStateCannotMutateInternalStorage()
    {
        var store = CreateStore();
        store.Store([CreateSnapshot(ProviderKind.Codex)]);

        var snapshots = store.GetCurrentSnapshots();

        Assert.IsNotType<Dictionary<ProviderKind, ProviderRuntimeSnapshotState>>(snapshots);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ProviderKind, ProviderRuntimeSnapshotState>)snapshots).Clear());
        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out _));
    }

    [Fact]
    public void FreshnessIsFreshBeforeThreshold()
    {
        var timeProvider = new ManualTimeProvider();
        var store = CreateStore(timeProvider, TimeSpan.FromMinutes(5));

        store.Store([CreateSnapshot(ProviderKind.Codex)]);
        timeProvider.Advance(TimeSpan.FromMinutes(4));

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(DataFreshness.Live, state.EffectiveFreshness);
    }

    [Fact]
    public void FreshnessBoundaryBehaviorIsDeterministic()
    {
        var timeProvider = new ManualTimeProvider();
        var store = CreateStore(timeProvider, TimeSpan.FromMinutes(5));

        store.Store([CreateSnapshot(ProviderKind.Codex)]);
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(DataFreshness.Live, state.EffectiveFreshness);
    }

    [Fact]
    public void FreshnessBecomesStaleAfterThreshold()
    {
        var timeProvider = new ManualTimeProvider();
        var store = CreateStore(timeProvider, TimeSpan.FromMinutes(5));

        store.Store([CreateSnapshot(ProviderKind.Codex)]);
        timeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromTicks(1)));

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(DataFreshness.Stale, state.EffectiveFreshness);
    }

    [Fact]
    public void UnknownUnavailableFreshnessIsNotFalselyUpgraded()
    {
        var store = CreateStore();

        store.Store([CreateUnavailableSnapshot(ProviderKind.Codex)]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(DataFreshness.Unknown, state.EffectiveFreshness);
    }

    [Fact]
    public void NormalizedMetricsContributeToFreshness()
    {
        var store = CreateStore();
        var capturedAt = DateTimeOffset.UtcNow;
        var snapshot = new ProviderUsageSnapshot(
            ProviderKind.Codex,
            ProviderConnectionState.Connected,
            PercentageUsageMetric.Unavailable(DataAuthority.LocalProviderReported, DataFreshness.Unknown),
            null,
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            capturedAt,
            [
                new NormalizedQuotaWindow(
                    ProviderKind.Codex,
                    "codex:default:primary",
                    null,
                    UsageMetricLabelOrigin.Unknown,
                    25m,
                    capturedAt.AddHours(1),
                    TimeSpan.FromHours(5),
                    UsageMetricAvailability.Available,
                    capturedAt,
                    DataAuthority.LocalProviderReported,
                    DataFreshness.Live,
                    "account/rateLimits/read")
            ],
            []);

        store.Store([snapshot]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(DataFreshness.Live, state.EffectiveFreshness);
    }

    [Fact]
    public void ProviderReportedStaleFreshnessIsNotFalselyUpgraded()
    {
        var store = CreateStore();

        store.Store([CreateSnapshot(ProviderKind.Codex, freshness: DataFreshness.Stale)]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(DataFreshness.Stale, state.EffectiveFreshness);
    }

    [Fact]
    public void FreshnessIsComputedAtReadTime()
    {
        var timeProvider = new ManualTimeProvider();
        var store = CreateStore(timeProvider, TimeSpan.FromMinutes(5));

        store.Store([CreateSnapshot(ProviderKind.Codex)]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var freshState));
        Assert.Equal(DataFreshness.Live, freshState.EffectiveFreshness);

        timeProvider.Advance(TimeSpan.FromMinutes(6));

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var staleState));
        Assert.Equal(DataFreshness.Stale, staleState.EffectiveFreshness);
    }

    [Fact]
    public void CurrentCodexCollectionUsesAcceptanceTimeNotMetricCaptureTime()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var store = CreateStore(timeProvider, TimeSpan.FromMinutes(5));

        store.Store(
        [
            CreateSnapshot(
                ProviderKind.Codex,
                capturedAt: timeProvider.GetUtcNow().AddHours(-2))
        ]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(DataFreshness.Live, state.EffectiveFreshness);
    }

    [Fact]
    public void RereadingUnchangedSourceObservationDoesNotExtendFreshness()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var store = CreateStore(timeProvider, TimeSpan.FromMinutes(5));
        var snapshot = CreateSnapshot(
            ProviderKind.Claude,
            capturedAt: timeProvider.GetUtcNow(),
            sourceObservedAt: timeProvider.GetUtcNow());

        store.Store([snapshot]);
        timeProvider.Advance(TimeSpan.FromMinutes(4));
        store.Store([snapshot]);
        timeProvider.Advance(TimeSpan.FromMinutes(2));

        Assert.True(store.TryGetCurrent(ProviderKind.Claude, out var state));
        Assert.Equal(DataFreshness.Stale, state.EffectiveFreshness);
    }

    [Fact]
    public void NewSourceObservationAdvancesFreshness()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var store = CreateStore(timeProvider, TimeSpan.FromMinutes(5));

        store.Store(
        [
            CreateSnapshot(
                ProviderKind.Claude,
                sourceObservedAt: timeProvider.GetUtcNow())
        ]);
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        store.Store(
        [
            CreateSnapshot(
                ProviderKind.Claude,
                sourceObservedAt: timeProvider.GetUtcNow())
        ]);

        Assert.True(store.TryGetCurrent(ProviderKind.Claude, out var state));
        Assert.Equal(DataFreshness.Live, state.EffectiveFreshness);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidSourceObservationFallsBackToAcceptanceTime(bool isFutureTimestamp)
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var store = CreateStore(timeProvider, TimeSpan.FromMinutes(5));
        var sourceObservedAt = isFutureTimestamp
            ? timeProvider.GetUtcNow().AddMinutes(1)
            : DateTimeOffset.MinValue;

        store.Store(
        [
            CreateSnapshot(
                ProviderKind.Claude,
                sourceObservedAt: sourceObservedAt)
        ]);
        timeProvider.Advance(TimeSpan.FromMinutes(6));

        Assert.True(store.TryGetCurrent(ProviderKind.Claude, out var state));
        Assert.Equal(DataFreshness.Stale, state.EffectiveFreshness);
    }

    [Fact]
    public void SafeDisconnectedNormalizedSnapshotIsStoredWithoutExceptionText()
    {
        var store = CreateStore();

        store.Store([CreateUnavailableSnapshot(ProviderKind.Codex)]);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(ProviderConnectionState.Disconnected, state.Snapshot.ConnectionState);
        Assert.DoesNotContain(
            typeof(ProviderRuntimeSnapshotState).GetProperties(),
            property => property.Name.Contains("Exception", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CancellationPreservesPriorState()
    {
        var store = CreateStore();
        var collector = new SequencedProviderUsageCollector(
            ProviderKind.Codex,
            _ => Task.FromResult(CreateSnapshot(ProviderKind.Codex, sessionTokens: 100)),
            cancellationToken => Task.FromCanceled<ProviderUsageSnapshot>(cancellationToken));
        await using var lifecycle = CreateLifecycle(collector, store);

        await lifecycle.RefreshAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            lifecycle.RefreshAsync(new CancellationToken(canceled: true)));

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(100, state.Snapshot.SessionTokens.TokenCount);
    }

    [Fact]
    public async Task UnexpectedExceptionPreservesPriorState()
    {
        var store = CreateStore();
        var collector = new SequencedProviderUsageCollector(
            ProviderKind.Codex,
            _ => Task.FromResult(CreateSnapshot(ProviderKind.Codex, sessionTokens: 100)),
            _ => Task.FromException<ProviderUsageSnapshot>(
                new InvalidOperationException("synthetic collector failure")));
        await using var lifecycle = CreateLifecycle(collector, store);

        await lifecycle.RefreshAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            lifecycle.RefreshAsync(CancellationToken.None));

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(100, state.Snapshot.SessionTokens.TokenCount);
    }

    [Fact]
    public async Task RefreshCompletionIncludesStoreUpdate()
    {
        var store = CreateStore();
        var collector = new SequencedProviderUsageCollector(
            ProviderKind.Codex,
            _ => Task.FromResult(CreateSnapshot(ProviderKind.Codex, sessionTokens: 100)));
        await using var lifecycle = CreateLifecycle(collector, store);

        await lifecycle.RefreshAsync(CancellationToken.None);

        Assert.True(store.TryGetCurrent(ProviderKind.Codex, out var state));
        Assert.Equal(100, state.Snapshot.SessionTokens.TokenCount);
    }

    [Fact]
    public void NoSnapshotFileOrPersistentStateIsCreated()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore();

        store.Store([CreateSnapshot(ProviderKind.Codex)]);

        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Fact]
    public void NoHistoricalListGrowsOverRepeatedRefreshes()
    {
        var store = CreateStore();

        for (var index = 0; index < 100; index++)
        {
            store.Store([CreateSnapshot(ProviderKind.Codex, sessionTokens: index)]);
        }

        var snapshots = store.GetCurrentSnapshots();

        Assert.Single(snapshots);
        Assert.Equal(99, snapshots[ProviderKind.Codex].Snapshot.SessionTokens.TokenCount);
    }

    private static InMemoryProviderRuntimeSnapshotStore CreateStore(
        ManualTimeProvider? timeProvider = null,
        TimeSpan? freshnessThreshold = null) =>
        new(
            timeProvider ?? new ManualTimeProvider(),
            freshnessThreshold ?? TimeSpan.FromMinutes(5));

    private static ProviderRefreshLifecycle CreateLifecycle(
        IProviderUsageCollector collector,
        IProviderRuntimeSnapshotStore store) =>
        new(
            new ProviderUsageCollectionCoordinator([collector]),
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            TimeSpan.FromMinutes(5),
            TimeProvider.System,
            static (_, _, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
            store);

    private static ProviderUsageSnapshot CreateSnapshot(
        ProviderKind provider,
        long sessionTokens = 100,
        DataFreshness freshness = DataFreshness.Live,
        DateTimeOffset? capturedAt = null,
        DateTimeOffset? sourceObservedAt = null) =>
        new(
            provider,
            ProviderConnectionState.Connected,
            new PercentageUsageMetric(
                50m,
                DataAuthority.LocalProviderReported,
                freshness),
            DateTimeOffset.UtcNow.AddHours(1),
            new TokenCountMetric(
                sessionTokens,
                DataAuthority.LocalProviderReported,
                freshness),
            new TokenCountMetric(
                1_000,
                DataAuthority.TokenFishDerived,
                freshness),
            capturedAt ?? DateTimeOffset.UtcNow,
            sourceObservedAt: sourceObservedAt);

    private static ProviderUsageSnapshot CreateUnavailableSnapshot(ProviderKind provider) =>
        new(
            provider,
            ProviderConnectionState.Disconnected,
            PercentageUsageMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            null,
            TokenCountMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            TokenCountMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            DateTimeOffset.UtcNow);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider()
            : this(new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero))
        {
        }

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
    }

    private sealed class SequencedProviderUsageCollector : IProviderUsageCollector
    {
        private readonly Queue<Func<CancellationToken, Task<ProviderUsageSnapshot>>> _responses;

        public SequencedProviderUsageCollector(
            ProviderKind provider,
            params Func<CancellationToken, Task<ProviderUsageSnapshot>>[] responses)
        {
            Provider = provider;
            _responses = new Queue<Func<CancellationToken, Task<ProviderUsageSnapshot>>>(responses);
        }

        public ProviderKind Provider { get; }

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            var response = _responses.Dequeue();
            return response(cancellationToken);
        }
    }

    private sealed class OutcomeProviderUsageCollector(ProviderCollectionResult result) : IProviderUsageCollector
    {
        public ProviderKind Provider => result.Snapshot.Provider;

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result.Snapshot);

        public Task<ProviderCollectionResult> CollectWithOutcomeAsync(
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
            Directory.CreateDirectory(path);
        }

        public string Path { get; }

        public static TemporaryDirectory Create() =>
            new(
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"TokenFish-{Guid.NewGuid():N}"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
