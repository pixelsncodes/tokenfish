using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexRuntimeFailureMappingCollectorTests
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 7, 12, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessfulSnapshotPassesThroughUnchanged()
    {
        var expected = CreateConnectedSnapshot();
        var innerCollector = new FakeProviderUsageCollector
        {
            Snapshot = expected
        };
        var collector = CreateCollector(innerCollector);

        var actual = await collector.CollectAsync(CancellationToken.None);

        Assert.Same(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ExpectedRuntimeFailures))]
    public async Task ExpectedRuntimeFailureMapsToSafeUnavailableSnapshot(Exception exception)
    {
        var innerCollector = new FakeProviderUsageCollector
        {
            CollectException = exception
        };
        var collector = CreateCollector(innerCollector);

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        AssertSafeUnavailableSnapshot(snapshot);
    }

    [Theory]
    [MemberData(nameof(ExpectedRuntimeFailuresWithReasons))]
    public async Task ExpectedRuntimeFailureRecordsStableReason(
        Exception exception,
        ProviderCollectionFailureReason expectedReason)
    {
        var collector = CreateCollector(new FakeProviderUsageCollector { CollectException = exception });

        var result = await collector.CollectWithOutcomeAsync(CancellationToken.None);

        Assert.Equal(ProviderCollectionOutcome.Failed, result.Outcome);
        Assert.Equal(expectedReason, result.FailureReason);
    }

    [Fact]
    public async Task MappedSnapshotContainsNoFabricatedUsageValuesOrResetTimestamp()
    {
        var innerCollector = new FakeProviderUsageCollector
        {
            CollectException = new CodexAppServerProtocolException("sanitized failure")
        };
        var collector = CreateCollector(innerCollector);

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.False(snapshot.UsageWindow.IsAvailable);
        Assert.Null(snapshot.UsageWindow.PercentageConsumed);
        Assert.Null(snapshot.UsageWindowResetAt);
        Assert.False(snapshot.WeeklyTokens.IsAvailable);
        Assert.Null(snapshot.WeeklyTokens.TokenCount);
        Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.Null(snapshot.SessionTokens.TokenCount);
    }

    [Fact]
    public async Task UnavailableMetricsUseUnknownFreshness()
    {
        var innerCollector = new FakeProviderUsageCollector
        {
            CollectException = new CodexAppServerProtocolException("sanitized failure")
        };
        var collector = CreateCollector(innerCollector);

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(DataFreshness.Unknown, snapshot.UsageWindow.Freshness);
        Assert.Equal(DataFreshness.Unknown, snapshot.WeeklyTokens.Freshness);
        Assert.Equal(DataFreshness.Unknown, snapshot.SessionTokens.Freshness);
    }

    [Fact]
    public async Task MappedSnapshotUsesDisconnectedConnectionState()
    {
        var innerCollector = new FakeProviderUsageCollector
        {
            CollectException = new CodexAppServerProtocolException("sanitized failure")
        };
        var collector = CreateCollector(innerCollector);

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderConnectionState.Disconnected, snapshot.ConnectionState);
    }

    [Fact]
    public async Task OperationCanceledExceptionPropagatesUnchanged()
    {
        using var cancellation = new CancellationTokenSource();
        var expected = new OperationCanceledException(cancellation.Token);
        var innerCollector = new FakeProviderUsageCollector
        {
            CollectException = expected
        };
        var collector = CreateCollector(innerCollector);

        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            collector.CollectAsync(cancellation.Token));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task UnexpectedExceptionPropagatesUnchanged()
    {
        var expected = new InvalidOperationException("programming failure");
        var innerCollector = new FakeProviderUsageCollector
        {
            CollectException = expected
        };
        var collector = CreateCollector(innerCollector);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            collector.CollectAsync(CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void ConstructionDoesNotCollect()
    {
        var innerCollector = new FakeProviderUsageCollector();

        _ = CreateCollector(innerCollector);

        Assert.Equal(0, innerCollector.CollectCallCount);
    }

    [Fact]
    public void ReadingProviderDoesNotCollect()
    {
        var innerCollector = new FakeProviderUsageCollector();
        var collector = CreateCollector(innerCollector);

        _ = collector.Provider;

        Assert.Equal(0, innerCollector.CollectCallCount);
    }

    public static TheoryData<Exception> ExpectedRuntimeFailures() =>
        new()
        {
            new CodexAppServerSessionException("sanitized startup failure"),
            new CodexAppServerProtocolException("sanitized protocol failure"),
            new CodexRateLimitsResponseParseException("sanitized rate-limit parse failure"),
            new CodexRateLimitsResponseParseException("sanitized rate-limit JSON-RPC failure", 123),
            new CodexAccountUsageResponseParseException("sanitized account usage parse failure"),
            new CodexAccountUsageResponseParseException(
                "sanitized account usage JSON-RPC failure",
                123),
            new CodexUsageSnapshotNormalizationException("sanitized normalization failure")
        };

    public static TheoryData<Exception, ProviderCollectionFailureReason> ExpectedRuntimeFailuresWithReasons() =>
        new()
        {
            { new CodexAppServerSessionException("sanitized startup failure"), ProviderCollectionFailureReason.CodexSession },
            { new CodexAppServerProtocolException("sanitized protocol failure"), ProviderCollectionFailureReason.CodexProtocol },
            { new CodexRateLimitsResponseParseException("sanitized rate-limit parse failure"), ProviderCollectionFailureReason.CodexRateLimitsResponse },
            { new CodexAccountUsageResponseParseException("sanitized account usage parse failure"), ProviderCollectionFailureReason.CodexAccountUsageResponse },
            { new CodexUsageSnapshotNormalizationException("sanitized normalization failure"), ProviderCollectionFailureReason.CodexUsageNormalization }
        };

    private static CodexRuntimeFailureMappingCollector CreateCollector(
        FakeProviderUsageCollector innerCollector) =>
        new(innerCollector, new FakeTimeProvider(CapturedAt));

    private static ProviderUsageSnapshot CreateConnectedSnapshot() =>
        new(
            ProviderKind.Codex,
            ProviderConnectionState.Connected,
            new PercentageUsageMetric(42, DataAuthority.LocalProviderReported, DataFreshness.Live),
            CapturedAt.AddHours(1),
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            new TokenCountMetric(123, DataAuthority.TokenFishDerived, DataFreshness.Live),
            CapturedAt);

    private static void AssertSafeUnavailableSnapshot(ProviderUsageSnapshot snapshot)
    {
        Assert.Equal(ProviderKind.Codex, snapshot.Provider);
        Assert.Equal(ProviderConnectionState.Disconnected, snapshot.ConnectionState);
        Assert.False(snapshot.UsageWindow.IsAvailable);
        Assert.Equal(DataAuthority.LocalProviderReported, snapshot.UsageWindow.Authority);
        Assert.Equal(DataFreshness.Unknown, snapshot.UsageWindow.Freshness);
        Assert.Null(snapshot.UsageWindowResetAt);
        Assert.False(snapshot.WeeklyTokens.IsAvailable);
        Assert.Equal(DataAuthority.TokenFishDerived, snapshot.WeeklyTokens.Authority);
        Assert.Equal(DataFreshness.Unknown, snapshot.WeeklyTokens.Freshness);
        Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.Equal(DataAuthority.TokenFishDerived, snapshot.SessionTokens.Authority);
        Assert.Equal(DataFreshness.Unknown, snapshot.SessionTokens.Freshness);
        Assert.Equal(CapturedAt, snapshot.CapturedAt);
    }

    private sealed class FakeProviderUsageCollector : IProviderUsageCollector
    {
        public ProviderUsageSnapshot Snapshot { get; init; } = CreateConnectedSnapshot();

        public Exception? CollectException { get; init; }

        public int CollectCallCount { get; private set; }

        public ProviderKind Provider => ProviderKind.Codex;

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            CollectCallCount++;

            if (CollectException is not null)
            {
                return Task.FromException<ProviderUsageSnapshot>(CollectException);
            }

            return Task.FromResult(Snapshot);
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
