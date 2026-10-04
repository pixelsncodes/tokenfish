using TokenFish.Core.Models;
using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexProviderUsageCollectorTests
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 7, 12, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ProviderIsCodex()
    {
        var collector = CreateCollector();

        Assert.Equal(ProviderKind.Codex, collector.Provider);
    }

    [Fact]
    public async Task CollectRequestsRateLimitsAndAccountUsageExactlyOnce()
    {
        var protocolClient = new FakeCodexAppServerProtocolClient();
        var collector = CreateCollector(protocolClient);

        await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(1, protocolClient.RateLimitsCallCount);
        Assert.Equal(1, protocolClient.AccountUsageCallCount);
    }

    [Fact]
    public async Task CollectRequestsRateLimitsBeforeAccountUsage()
    {
        var protocolClient = new FakeCodexAppServerProtocolClient();
        var collector = CreateCollector(protocolClient);

        await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(["rateLimits", "accountUsage"], protocolClient.Operations);
    }

    [Fact]
    public async Task AccountUsageReceivesACancellableDeadlineWhileQuotaUsesCallerToken()
    {
        var protocolClient = new FakeCodexAppServerProtocolClient();
        var collector = CreateCollector(protocolClient);
        using var cancellation = new CancellationTokenSource();

        await collector.CollectAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, protocolClient.RateLimitsCancellationToken);
        Assert.True(protocolClient.AccountUsageCancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task CollectReturnsCompleteNormalizedSnapshot()
    {
        var resetAt = new DateTimeOffset(2026, 7, 12, 16, 0, 0, TimeSpan.Zero);
        var protocolClient = new FakeCodexAppServerProtocolClient
        {
            RateLimitsResult = CreateRateLimitsSnapshot(73, resetAt),
            AccountUsageResult = CreateAccountUsageSnapshot(
            [
                Bucket(2026, 7, 6, 10),
                Bucket(2026, 7, 7, 20),
                Bucket(2026, 7, 12, 70),
                Bucket(2026, 7, 5, 999)
            ])
        };
        var collector = CreateCollector(protocolClient);

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderKind.Codex, snapshot.Provider);
        Assert.Equal(ProviderConnectionState.Connected, snapshot.ConnectionState);
        Assert.True(snapshot.UsageWindow.IsAvailable);
        Assert.Equal(73m, snapshot.UsageWindow.PercentageConsumed);
        Assert.Equal(resetAt, snapshot.UsageWindowResetAt);
        Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.True(snapshot.WeeklyTokens.IsAvailable);
        Assert.Equal(100, snapshot.WeeklyTokens.TokenCount);
        Assert.Equal(CapturedAt, snapshot.CapturedAt);
    }

    [Fact]
    public async Task CaptureTimestampComesFromInjectedTimeProvider()
    {
        var capturedAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(capturedAt);
        var collector = CreateCollector(timeProvider: timeProvider);

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(capturedAt, snapshot.CapturedAt);
        Assert.Equal(1, timeProvider.GetUtcNowCallCount);
    }

    [Fact]
    public async Task CaptureTimeIsObtainedAfterBothReadsComplete()
    {
        var protocolClient = new FakeCodexAppServerProtocolClient();
        var timeProvider = new FakeTimeProvider(CapturedAt)
        {
            OnGetUtcNow = () =>
            {
                Assert.True(protocolClient.RateLimitsCompleted);
                Assert.True(protocolClient.AccountUsageCompleted);
            }
        };
        var collector = CreateCollector(protocolClient, timeProvider);

        await collector.CollectAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RateLimitFailurePropagates()
    {
        var expected = new InvalidOperationException("rate limit failure");
        var protocolClient = new FakeCodexAppServerProtocolClient
        {
            RateLimitsException = expected
        };
        var collector = CreateCollector(protocolClient);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            collector.CollectAsync(CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task AccountUsageIsNotRequestedAfterRateLimitFailure()
    {
        var protocolClient = new FakeCodexAppServerProtocolClient
        {
            RateLimitsException = new InvalidOperationException("rate limit failure")
        };
        var collector = CreateCollector(protocolClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            collector.CollectAsync(CancellationToken.None));

        Assert.Equal(0, protocolClient.AccountUsageCallCount);
    }

    [Fact]
    public async Task AccountUsageFailurePropagates()
    {
        var expected = new InvalidOperationException("account usage failure");
        var protocolClient = new FakeCodexAppServerProtocolClient
        {
            AccountUsageException = expected
        };
        var collector = CreateCollector(protocolClient);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            collector.CollectAsync(CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task CancellationFromRateLimitReadPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var expected = new OperationCanceledException(cancellation.Token);
        var protocolClient = new FakeCodexAppServerProtocolClient
        {
            RateLimitsException = expected
        };
        var collector = CreateCollector(protocolClient);

        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            collector.CollectAsync(cancellation.Token));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task CancellationFromAccountUsageReadPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var expected = new OperationCanceledException(cancellation.Token);
        var protocolClient = new FakeCodexAppServerProtocolClient
        {
            AccountUsageException = expected
        };
        var collector = CreateCollector(protocolClient);

        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            collector.CollectAsync(cancellation.Token));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task NoPartialSnapshotIsReturnedAfterFailure()
    {
        var protocolClient = new FakeCodexAppServerProtocolClient
        {
            AccountUsageException = new InvalidOperationException("account usage failure")
        };
        var collector = CreateCollector(protocolClient);
        ProviderUsageSnapshot? snapshot = null;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            snapshot = await collector.CollectAsync(CancellationToken.None));

        Assert.Null(snapshot);
    }

    [Fact]
    public void NullProtocolClientIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CodexProviderUsageCollector(
                null!,
                new CodexUsageSnapshotFactory()));
    }

    [Fact]
    public void NullSnapshotFactoryIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CodexProviderUsageCollector(
                new FakeCodexAppServerProtocolClient(),
                null!));
    }

    private static CodexProviderUsageCollector CreateCollector(
        FakeCodexAppServerProtocolClient? protocolClient = null,
        FakeTimeProvider? timeProvider = null) =>
        new(
            protocolClient ?? new FakeCodexAppServerProtocolClient(),
            new CodexUsageSnapshotFactory(),
            timeProvider ?? new FakeTimeProvider(CapturedAt));

    private static CodexRateLimitsSnapshot CreateRateLimitsSnapshot(
        int? primaryUsedPercent = 42,
        DateTimeOffset? primaryResetAt = null) =>
        new(
            new CodexRateLimitWindow(primaryUsedPercent, primaryUsedPercent.HasValue ? 300 : null, primaryResetAt),
            new CodexRateLimitWindow(21, 10_080, null),
            null);

    private static CodexAccountUsageSnapshot CreateAccountUsageSnapshot(
        IReadOnlyList<CodexDailyTokenUsage>? dailyUsageBuckets = null) =>
        new(dailyUsageBuckets ?? [Bucket(2026, 7, 12, 100)]);

    private static CodexDailyTokenUsage Bucket(int year, int month, int day, long tokens) =>
        new(new DateOnly(year, month, day), tokens);

    private sealed class FakeCodexAppServerProtocolClient : ICodexAppServerProtocolClient
    {
        public CodexRateLimitsSnapshot RateLimitsResult { get; init; } = CreateRateLimitsSnapshot();

        public CodexAccountUsageSnapshot AccountUsageResult { get; init; } =
            CreateAccountUsageSnapshot();

        public Exception? RateLimitsException { get; init; }

        public Exception? AccountUsageException { get; init; }

        public int RateLimitsCallCount { get; private set; }

        public int AccountUsageCallCount { get; private set; }

        public CancellationToken RateLimitsCancellationToken { get; private set; }

        public CancellationToken AccountUsageCancellationToken { get; private set; }

        public List<string> Operations { get; } = [];

        public bool RateLimitsCompleted { get; private set; }

        public bool AccountUsageCompleted { get; private set; }

        public Task<CodexRateLimitsSnapshot> ReadRateLimitsAsync(
            CancellationToken cancellationToken)
        {
            RateLimitsCallCount++;
            RateLimitsCancellationToken = cancellationToken;
            Operations.Add("rateLimits");

            if (RateLimitsException is not null)
            {
                return Task.FromException<CodexRateLimitsSnapshot>(RateLimitsException);
            }

            RateLimitsCompleted = true;
            return Task.FromResult(RateLimitsResult);
        }

        public Task<CodexAccountUsageSnapshot> ReadAccountUsageAsync(
            CancellationToken cancellationToken)
        {
            AccountUsageCallCount++;
            AccountUsageCancellationToken = cancellationToken;
            Operations.Add("accountUsage");

            if (AccountUsageException is not null)
            {
                return Task.FromException<CodexAccountUsageSnapshot>(AccountUsageException);
            }

            AccountUsageCompleted = true;
            return Task.FromResult(AccountUsageResult);
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public Action? OnGetUtcNow { get; init; }

        public int GetUtcNowCallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            GetUtcNowCallCount++;
            OnGetUtcNow?.Invoke();

            return utcNow;
        }
    }
}
