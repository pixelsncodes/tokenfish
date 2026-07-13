using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class OnboardingReadinessCoordinatorTests
{
    [Fact]
    public void ClaudeOnlyIgnoresCodexProblem()
    {
        var store = new FakeSnapshotStore(
            State(ProviderKind.Claude, ProviderCollectionOutcome.Succeeded),
            State(ProviderKind.Codex, ProviderCollectionOutcome.Failed, ProviderCollectionFailureReason.CodexProtocol));

        var result = CreateCoordinator(store).Evaluate(Settings(ProviderSelectionMode.ClaudeOnly));

        Assert.Equal(OnboardingReadinessState.Ready, result.State);
        Assert.Equal(ProviderKind.Claude, Assert.Single(result.Providers).Provider);
    }

    [Fact]
    public void CodexOnlyIgnoresClaudeProblem()
    {
        var store = new FakeSnapshotStore(
            State(ProviderKind.Claude, ProviderCollectionOutcome.Failed, ProviderCollectionFailureReason.ClaudeBridgeMalformed),
            State(ProviderKind.Codex, ProviderCollectionOutcome.Succeeded));

        var result = CreateCoordinator(store).Evaluate(Settings(ProviderSelectionMode.CodexOnly));

        Assert.Equal(OnboardingReadinessState.Ready, result.State);
        Assert.Equal(ProviderKind.Codex, Assert.Single(result.Providers).Provider);
    }

    [Fact]
    public void BothEvaluatesBothProviders()
    {
        var result = CreateCoordinator(new FakeSnapshotStore(
            State(ProviderKind.Claude, ProviderCollectionOutcome.Succeeded),
            State(ProviderKind.Codex, ProviderCollectionOutcome.NoObservation)))
            .Evaluate(Settings(ProviderSelectionMode.Both));

        Assert.Equal([ProviderKind.Claude, ProviderKind.Codex], result.Providers.Select(provider => provider.Provider));
        Assert.Equal(OnboardingReadinessState.Waiting, result.State);
    }

    [Theory]
    [InlineData(DataFreshness.Live)]
    [InlineData(DataFreshness.Cached)]
    public void ClaudeSucceededWithUsableDataIsReady(DataFreshness freshness)
    {
        var result = CreateCoordinator(new FakeSnapshotStore(
            State(ProviderKind.Claude, ProviderCollectionOutcome.Succeeded, freshness: freshness)))
            .Evaluate(Settings(ProviderSelectionMode.ClaudeOnly));

        var provider = Assert.Single(result.Providers);
        Assert.Equal(OnboardingReadinessState.Ready, provider.State);
        Assert.Equal(OnboardingReadinessReason.ProviderReady, provider.Reason);
    }

    [Fact]
    public void ClaudeNoObservationAndMissingStateAreWaiting()
    {
        var noObservation = CreateCoordinator(new FakeSnapshotStore(
            State(ProviderKind.Claude, ProviderCollectionOutcome.NoObservation)))
            .Evaluate(Settings(ProviderSelectionMode.ClaudeOnly));
        var missing = CreateCoordinator(new FakeSnapshotStore())
            .Evaluate(Settings(ProviderSelectionMode.ClaudeOnly));

        Assert.All([Assert.Single(noObservation.Providers), Assert.Single(missing.Providers)], provider =>
        {
            Assert.Equal(OnboardingReadinessState.Waiting, provider.State);
            Assert.Equal(OnboardingReadinessReason.AwaitingFirstObservation, provider.Reason);
        });
    }

    [Theory]
    [InlineData(ProviderCollectionFailureReason.ClaudeBridgeMalformed)]
    [InlineData(ProviderCollectionFailureReason.ClaudeBridgeUnreadable)]
    public void ClaudeFailureIsProblemWithTypedReason(ProviderCollectionFailureReason reason)
    {
        var result = CreateCoordinator(new FakeSnapshotStore(
            State(ProviderKind.Claude, ProviderCollectionOutcome.Failed, reason)))
            .Evaluate(Settings(ProviderSelectionMode.ClaudeOnly));

        var provider = Assert.Single(result.Providers);
        Assert.Equal(OnboardingReadinessState.Problem, provider.State);
        Assert.Equal(OnboardingReadinessReason.CollectionFailed, provider.Reason);
        Assert.Equal(reason, provider.CollectionFailureReason);
    }

    [Fact]
    public void CodexSucceededWithUsableDataIsReady()
    {
        var result = CreateCoordinator(new FakeSnapshotStore(
            State(ProviderKind.Codex, ProviderCollectionOutcome.Succeeded)))
            .Evaluate(Settings(ProviderSelectionMode.CodexOnly));

        Assert.Equal(OnboardingReadinessState.Ready, result.State);
    }

    [Fact]
    public void CodexNoObservationIsWaiting()
    {
        var result = CreateCoordinator(new FakeSnapshotStore(
            State(ProviderKind.Codex, ProviderCollectionOutcome.NoObservation)))
            .Evaluate(Settings(ProviderSelectionMode.CodexOnly));

        Assert.Equal(OnboardingReadinessState.Waiting, result.State);
        Assert.Equal(OnboardingReadinessReason.AwaitingFirstObservation, Assert.Single(result.Providers).Reason);
    }

    [Theory]
    [InlineData(ProviderCollectionFailureReason.CodexSession)]
    [InlineData(ProviderCollectionFailureReason.CodexProtocol)]
    [InlineData(ProviderCollectionFailureReason.CodexRateLimitsResponse)]
    [InlineData(ProviderCollectionFailureReason.CodexAccountUsageResponse)]
    [InlineData(ProviderCollectionFailureReason.CodexUsageNormalization)]
    public void CodexFailureIsProblemWithTypedReason(ProviderCollectionFailureReason reason)
    {
        var result = CreateCoordinator(new FakeSnapshotStore(
            State(ProviderKind.Codex, ProviderCollectionOutcome.Failed, reason)))
            .Evaluate(Settings(ProviderSelectionMode.CodexOnly));

        var provider = Assert.Single(result.Providers);
        Assert.Equal(OnboardingReadinessState.Problem, provider.State);
        Assert.Equal(reason, provider.CollectionFailureReason);
    }

    [Fact]
    public void OverallProblemTakesPrecedenceOverReadyAndWaiting()
    {
        var result = CreateCoordinator(new FakeSnapshotStore(
            State(ProviderKind.Claude, ProviderCollectionOutcome.Succeeded),
            State(ProviderKind.Codex, ProviderCollectionOutcome.Failed, ProviderCollectionFailureReason.CodexProtocol)))
            .Evaluate(Settings(ProviderSelectionMode.Both));

        Assert.Equal(OnboardingReadinessState.Problem, result.State);
    }

    [Fact]
    public void EvaluationDoesNotMutateAuthoritativeState()
    {
        var state = State(ProviderKind.Claude, ProviderCollectionOutcome.Succeeded);
        var store = new FakeSnapshotStore(state);
        var coordinator = CreateCoordinator(store);

        _ = coordinator.Evaluate(Settings(ProviderSelectionMode.ClaudeOnly));
        _ = coordinator.Evaluate(Settings(ProviderSelectionMode.ClaudeOnly));

        Assert.Same(state, store.GetState(ProviderKind.Claude));
        Assert.Equal(state.AcceptedAt, store.GetState(ProviderKind.Claude).AcceptedAt);
        Assert.Equal(state.Snapshot.SourceObservedAt, store.GetState(ProviderKind.Claude).Snapshot.SourceObservedAt);
    }

    [Fact]
    public async Task RecheckRefreshesOnceThenUsesUpdatedAuthoritativeState()
    {
        var store = new FakeSnapshotStore(State(ProviderKind.Codex, ProviderCollectionOutcome.NoObservation));
        var lifecycle = new FakeRefreshLifecycle(() =>
            store.Set(State(ProviderKind.Codex, ProviderCollectionOutcome.Succeeded)));
        var coordinator = new OnboardingReadinessCoordinator(store, lifecycle);

        var result = await coordinator.RecheckAsync(Settings(ProviderSelectionMode.CodexOnly), CancellationToken.None);

        Assert.Equal(1, lifecycle.RefreshCallCount);
        Assert.Equal(OnboardingReadinessState.Ready, result.State);
    }

    [Fact]
    public async Task RecheckReturnsAuthoritativeCollectionProblem()
    {
        var store = new FakeSnapshotStore();
        var lifecycle = new FakeRefreshLifecycle(() => store.Set(
            State(
                ProviderKind.Codex,
                ProviderCollectionOutcome.Failed,
                ProviderCollectionFailureReason.CodexProtocol)));
        var coordinator = new OnboardingReadinessCoordinator(store, lifecycle);

        var result = await coordinator.RecheckAsync(Settings(ProviderSelectionMode.CodexOnly), CancellationToken.None);

        var provider = Assert.Single(result.Providers);
        Assert.Equal(OnboardingReadinessState.Problem, result.State);
        Assert.Equal(ProviderCollectionFailureReason.CodexProtocol, provider.CollectionFailureReason);
    }

    [Fact]
    public async Task RecheckPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var lifecycle = new FakeRefreshLifecycle();
        var coordinator = new OnboardingReadinessCoordinator(new FakeSnapshotStore(), lifecycle);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.RecheckAsync(Settings(ProviderSelectionMode.CodexOnly), cancellation.Token));
        Assert.Equal(1, lifecycle.RefreshCallCount);
    }

    private static OnboardingReadinessCoordinator CreateCoordinator(FakeSnapshotStore store) =>
        new(store, new FakeRefreshLifecycle());

    private static AppSettings Settings(ProviderSelectionMode mode) => new() { ProviderSelectionMode = mode };

    private static ProviderRuntimeSnapshotState State(
        ProviderKind provider,
        ProviderCollectionOutcome outcome,
        ProviderCollectionFailureReason? failureReason = null,
        DataFreshness freshness = DataFreshness.Live) =>
        new(
            new ProviderUsageSnapshot(
                provider,
                ProviderConnectionState.Connected,
                new PercentageUsageMetric(25m, DataAuthority.LocalProviderReported, freshness),
                null,
                TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
                TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
                DateTimeOffset.UtcNow,
                sourceObservedAt: DateTimeOffset.UtcNow.AddMinutes(-1)),
            DateTimeOffset.UtcNow,
            freshness,
            outcome,
            failureReason);

    private sealed class FakeSnapshotStore : IProviderRuntimeSnapshotStore
    {
        private readonly Dictionary<ProviderKind, ProviderRuntimeSnapshotState> _states;

        public FakeSnapshotStore(params ProviderRuntimeSnapshotState[] states) =>
            _states = states.ToDictionary(state => state.Snapshot.Provider);

        public void Store(IReadOnlyList<ProviderUsageSnapshot> snapshots) => throw new NotSupportedException();

        public void Store(IReadOnlyList<ProviderCollectionResult> results) => throw new NotSupportedException();

        public bool TryGetCurrent(ProviderKind provider, out ProviderRuntimeSnapshotState currentState) =>
            _states.TryGetValue(provider, out currentState!);

        public IReadOnlyDictionary<ProviderKind, ProviderRuntimeSnapshotState> GetCurrentSnapshots() => _states;

        public ProviderRuntimeSnapshotState GetState(ProviderKind provider) => _states[provider];

        public void Set(ProviderRuntimeSnapshotState state) => _states[state.Snapshot.Provider] = state;
    }

    private sealed class FakeRefreshLifecycle(Action? onRefresh = null) : IProviderRefreshLifecycle
    {
        public int RefreshCallCount { get; private set; }

        public ProviderRefreshStatus RefreshStatus => ProviderRefreshStatus.Initial;

        public event Action<ProviderRefreshStatus>? RefreshStatusChanged
        {
            add { }
            remove { }
        }

        public Task Completion => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ProviderUsageSnapshot>> RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            onRefresh?.Invoke();
            return Task.FromResult<IReadOnlyList<ProviderUsageSnapshot>>([]);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
