using System.Globalization;
using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Core.Tests;

public sealed class ProviderUsageContractTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("100")]
    public void PercentageMetricAcceptsValidBoundaries(string percentageText)
    {
        var percentage = decimal.Parse(percentageText, CultureInfo.InvariantCulture);

        var metric = new PercentageUsageMetric(
            percentage,
            DataAuthority.LocalProviderReported,
            DataFreshness.Live);

        Assert.True(metric.IsAvailable);
        Assert.Equal(percentage, metric.PercentageConsumed);
        Assert.Equal(DataAuthority.LocalProviderReported, metric.Authority);
        Assert.Equal(DataFreshness.Live, metric.Freshness);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    public void PercentageMetricRejectsInvalidValues(string percentageText)
    {
        var percentage = decimal.Parse(percentageText, CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PercentageUsageMetric(
                percentage,
                DataAuthority.LocalProviderReported,
                DataFreshness.Live));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public void TokenCountMetricAcceptsZeroAndPositiveCounts(long tokenCount)
    {
        var metric = new TokenCountMetric(
            tokenCount,
            DataAuthority.LocalProviderReported,
            DataFreshness.Live);

        Assert.True(metric.IsAvailable);
        Assert.Equal(tokenCount, metric.TokenCount);
        Assert.Equal(DataAuthority.LocalProviderReported, metric.Authority);
        Assert.Equal(DataFreshness.Live, metric.Freshness);
    }

    [Fact]
    public void TokenCountMetricRejectsNegativeCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TokenCountMetric(
                -1,
                DataAuthority.LocalProviderReported,
                DataFreshness.Live));
    }

    [Fact]
    public void PercentageMetricCanBeExplicitlyUnavailable()
    {
        var metric = PercentageUsageMetric.Unavailable(
            DataAuthority.TokenFishDerived,
            DataFreshness.Unknown);

        Assert.False(metric.IsAvailable);
        Assert.Null(metric.PercentageConsumed);
        Assert.Equal(DataAuthority.TokenFishDerived, metric.Authority);
        Assert.Equal(DataFreshness.Unknown, metric.Freshness);
    }

    [Fact]
    public void TokenCountMetricCanBeExplicitlyUnavailable()
    {
        var metric = TokenCountMetric.Unavailable(
            DataAuthority.TokenFishDerived,
            DataFreshness.Unknown);

        Assert.False(metric.IsAvailable);
        Assert.Null(metric.TokenCount);
        Assert.Equal(DataAuthority.TokenFishDerived, metric.Authority);
        Assert.Equal(DataFreshness.Unknown, metric.Freshness);
    }

    [Fact]
    public void ProviderUsageSnapshotCapturesCompleteProviderState()
    {
        var resetAt = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var capturedAt = new DateTimeOffset(2026, 7, 12, 7, 30, 0, TimeSpan.Zero);
        var usageWindow = new PercentageUsageMetric(
            65.5m,
            DataAuthority.LocalProviderReported,
            DataFreshness.Live);
        var sessionTokens = new TokenCountMetric(
            12_345,
            DataAuthority.LocalProviderReported,
            DataFreshness.Live);
        var weeklyTokens = new TokenCountMetric(
            98_765,
            DataAuthority.TokenFishDerived,
            DataFreshness.Cached);
        var quotaWindow = new NormalizedQuotaWindow(
            ProviderKind.Claude,
            "claude:synthetic:primary",
            null,
            UsageMetricLabelOrigin.Unknown,
            65.5m,
            resetAt,
            TimeSpan.FromHours(5),
            UsageMetricAvailability.Available,
            capturedAt,
            DataAuthority.LocalProviderReported,
            DataFreshness.Live,
            "synthetic/rateLimits");
        var activityMetric = new NormalizedActivityMetric(
            ProviderKind.Claude,
            "claude:activity:synthetic:tokens",
            null,
            UsageMetricLabelOrigin.Unknown,
            98_765,
            UsageActivityUnit.Tokens,
            capturedAt.AddDays(-6),
            capturedAt.AddDays(1),
            UsageMetricAvailability.Available,
            capturedAt,
            DataAuthority.TokenFishDerived,
            DataFreshness.Cached,
            "synthetic/usage");

        var snapshot = new ProviderUsageSnapshot(
            ProviderKind.Claude,
            ProviderConnectionState.Connected,
            usageWindow,
            resetAt,
            sessionTokens,
            weeklyTokens,
            capturedAt,
            [quotaWindow],
            [activityMetric]);

        Assert.Equal(ProviderKind.Claude, snapshot.Provider);
        Assert.Equal(ProviderConnectionState.Connected, snapshot.ConnectionState);
        Assert.Same(usageWindow, snapshot.UsageWindow);
        Assert.Equal(resetAt, snapshot.UsageWindowResetAt);
        Assert.Same(sessionTokens, snapshot.SessionTokens);
        Assert.Same(weeklyTokens, snapshot.WeeklyTokens);
        Assert.Equal([quotaWindow], snapshot.QuotaWindows);
        Assert.Equal([activityMetric], snapshot.ActivityMetrics);
        Assert.Equal(capturedAt, snapshot.CapturedAt);
    }

    [Fact]
    public void ProviderUsageSnapshotNormalizesTimestampsToUtc()
    {
        var resetAt = new DateTimeOffset(2026, 7, 12, 1, 0, 0, TimeSpan.FromHours(-7));
        var capturedAt = new DateTimeOffset(2026, 7, 12, 0, 30, 0, TimeSpan.FromHours(-7));
        var sourceObservedAt = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.FromHours(-7));

        var snapshot = new ProviderUsageSnapshot(
            ProviderKind.Codex,
            ProviderConnectionState.Degraded,
            PercentageUsageMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            resetAt,
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            capturedAt,
            sourceObservedAt: sourceObservedAt);

        Assert.Equal(TimeSpan.Zero, snapshot.UsageWindowResetAt?.Offset);
        Assert.Equal(resetAt.ToUniversalTime(), snapshot.UsageWindowResetAt);
        Assert.Equal(TimeSpan.Zero, snapshot.CapturedAt.Offset);
        Assert.Equal(capturedAt.ToUniversalTime(), snapshot.CapturedAt);
        Assert.Equal(TimeSpan.Zero, snapshot.SourceObservedAt?.Offset);
        Assert.Equal(sourceObservedAt.ToUniversalTime(), snapshot.SourceObservedAt);
    }

    [Fact]
    public async Task ProviderUsageCollectorReturnsNormalizedSnapshot()
    {
        var collector = new TestProviderUsageCollector();

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderKind.Codex, collector.Provider);
        Assert.Equal(ProviderKind.Codex, snapshot.Provider);
        Assert.False(snapshot.UsageWindow.IsAvailable);
        Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.False(snapshot.WeeklyTokens.IsAvailable);
    }

    private sealed class TestProviderUsageCollector : IProviderUsageCollector
    {
        public ProviderKind Provider => ProviderKind.Codex;

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new ProviderUsageSnapshot(
                    Provider,
                    ProviderConnectionState.NotConfigured,
                    PercentageUsageMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
                    null,
                    TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
                    TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
                    DateTimeOffset.UtcNow));
        }
    }
}
