using TokenFish.Core.Models;
using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexUsageSnapshotFactoryTests
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 7, 12, 15, 30, 0, TimeSpan.Zero);

    private readonly CodexUsageSnapshotFactory _factory = new();

    [Fact]
    public void ProviderIsCodex()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal(ProviderKind.Codex, snapshot.Provider);
    }

    [Fact]
    public void SuccessfulInputMapsToConnectedState()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal(ProviderConnectionState.Connected, snapshot.ConnectionState);
    }

    [Fact]
    public void PrimaryPercentageIsMapped()
    {
        var snapshot = CreateSnapshot(primaryUsedPercent: 73);

        Assert.True(snapshot.UsageWindow.IsAvailable);
        Assert.Equal(73m, snapshot.UsageWindow.PercentageConsumed);
    }

    [Fact]
    public void PrimaryPercentageAuthorityIsProviderReported()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal(DataAuthority.LocalProviderReported, snapshot.UsageWindow.Authority);
        Assert.Equal(DataFreshness.Live, snapshot.UsageWindow.Freshness);
    }

    [Fact]
    public void PrimaryResetTimestampIsMapped()
    {
        var resetAt = new DateTimeOffset(2026, 7, 12, 16, 0, 0, TimeSpan.Zero);

        var snapshot = CreateSnapshot(primaryResetAt: resetAt);

        Assert.Equal(resetAt, snapshot.UsageWindowResetAt);
    }

    [Fact]
    public void SecondaryResetTimestampDoesNotReplaceMissingPrimaryResetTimestamp()
    {
        var secondaryResetAt = new DateTimeOffset(2026, 7, 19, 16, 0, 0, TimeSpan.Zero);

        var snapshot = CreateSnapshot(secondaryResetAt: secondaryResetAt);

        Assert.Null(snapshot.UsageWindowResetAt);
    }

    [Fact]
    public void SecondaryPercentageDoesNotReplaceOrOverridePrimary()
    {
        var snapshot = CreateSnapshot(primaryUsedPercent: 12, secondaryUsedPercent: 99);

        Assert.Equal(12m, snapshot.UsageWindow.PercentageConsumed);
    }

    [Fact]
    public void MissingPrimaryPercentageProducesUnavailableUsageWindowMetric()
    {
        var snapshot = CreateSnapshot(primaryUsedPercent: null);

        Assert.False(snapshot.UsageWindow.IsAvailable);
        Assert.Null(snapshot.UsageWindow.PercentageConsumed);
        Assert.Equal(DataAuthority.LocalProviderReported, snapshot.UsageWindow.Authority);
        Assert.Equal(DataFreshness.Live, snapshot.UsageWindow.Freshness);
    }

    [Fact]
    public void CurrentSessionTokensAreUnavailable()
    {
        var snapshot = CreateSnapshot();

        Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.Null(snapshot.SessionTokens.TokenCount);
    }

    [Fact]
    public void SevenUtcCalendarDaysAreSummedCorrectly()
    {
        var snapshot = CreateSnapshot(
            dailyUsageBuckets:
            [
                Bucket(2026, 7, 6, 10),
                Bucket(2026, 7, 7, 20),
                Bucket(2026, 7, 8, 30),
                Bucket(2026, 7, 9, 40),
                Bucket(2026, 7, 10, 50),
                Bucket(2026, 7, 11, 60),
                Bucket(2026, 7, 12, 70),
                Bucket(2026, 7, 5, 999)
            ]);

        Assert.True(snapshot.WeeklyTokens.IsAvailable);
        Assert.Equal(280, snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void CaptureDateBucketIsIncluded()
    {
        var snapshot = CreateSnapshot(dailyUsageBuckets: [Bucket(2026, 7, 12, 123)]);

        Assert.Equal(123, snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void BucketExactlySixDaysBeforeCaptureIsIncluded()
    {
        var snapshot = CreateSnapshot(dailyUsageBuckets: [Bucket(2026, 7, 6, 123)]);

        Assert.Equal(123, snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void BucketSevenDaysBeforeCaptureIsExcluded()
    {
        var snapshot = CreateSnapshot(dailyUsageBuckets: [Bucket(2026, 7, 5, 123)]);

        Assert.Equal(0, snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void OlderBucketsAreExcluded()
    {
        var snapshot = CreateSnapshot(
            dailyUsageBuckets:
            [
                Bucket(2026, 7, 4, 100),
                Bucket(2026, 7, 3, 200)
            ]);

        Assert.Equal(0, snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void FutureBucketIsRejected()
    {
        var exception = Assert.Throws<CodexUsageSnapshotNormalizationException>(() =>
            CreateSnapshot(dailyUsageBuckets: [Bucket(2026, 7, 13, 1)]));

        Assert.DoesNotContain("2026-07-13", exception.Message);
    }

    [Fact]
    public void NullDailyBucketsProduceUnavailableWeeklyUsage()
    {
        var snapshot = CreateSnapshot(dailyUsageBucketsAvailable: false);

        Assert.False(snapshot.WeeklyTokens.IsAvailable);
        Assert.Null(snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void EmptyAvailableBucketsProduceWeeklyTotalZero()
    {
        var snapshot = CreateSnapshot(dailyUsageBuckets: []);

        Assert.True(snapshot.WeeklyTokens.IsAvailable);
        Assert.Equal(0, snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void WeeklyAuthorityIsLocallyCalculated()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal(DataAuthority.TokenFishDerived, snapshot.WeeklyTokens.Authority);
        Assert.Equal(DataFreshness.Live, snapshot.WeeklyTokens.Freshness);
    }

    [Fact]
    public void CheckedOverflowIsRejected()
    {
        var exception = Assert.Throws<CodexUsageSnapshotNormalizationException>(() =>
            CreateSnapshot(
                dailyUsageBuckets:
                [
                    Bucket(2026, 7, 11, long.MaxValue),
                    Bucket(2026, 7, 12, 1)
                ]));

        Assert.DoesNotContain(long.MaxValue.ToString(), exception.Message);
    }

    [Fact]
    public void NonUtcCaptureTimestampProducesCorrectUtcDateWindow()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 11, 18, 30, 0, TimeSpan.FromHours(-7));

        var snapshot = CreateSnapshot(
            capturedAt: capturedAt,
            dailyUsageBuckets:
            [
                Bucket(2026, 7, 6, 10),
                Bucket(2026, 7, 12, 20),
                Bucket(2026, 7, 5, 999)
            ]);

        Assert.Equal(30, snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void CaptureTimestampIsNormalizedConsistently()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 12, 8, 30, 0, TimeSpan.FromHours(-7));

        var snapshot = CreateSnapshot(capturedAt: capturedAt);

        Assert.Equal(TimeSpan.Zero, snapshot.CapturedAt.Offset);
        Assert.Equal(capturedAt.ToUniversalTime(), snapshot.CapturedAt);
    }

    [Fact]
    public void NullRateLimitsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _factory.Create(null!, CreateAccountUsageSnapshot(), CapturedAt));
    }

    [Fact]
    public void NullAccountUsageIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _factory.Create(CreateRateLimitsSnapshot(), null!, CapturedAt));
    }

    private ProviderUsageSnapshot CreateSnapshot(
        int? primaryUsedPercent = 42,
        int? secondaryUsedPercent = 21,
        DateTimeOffset? primaryResetAt = null,
        DateTimeOffset? secondaryResetAt = null,
        IReadOnlyList<CodexDailyTokenUsage>? dailyUsageBuckets = null,
        bool dailyUsageBucketsAvailable = true,
        DateTimeOffset? capturedAt = null)
    {
        var accountUsage = dailyUsageBuckets is null && dailyUsageBucketsAvailable
            ? CreateAccountUsageSnapshot([Bucket(2026, 7, 12, 100)])
            : CreateAccountUsageSnapshot(dailyUsageBuckets);

        return _factory.Create(
            CreateRateLimitsSnapshot(primaryUsedPercent, secondaryUsedPercent, primaryResetAt, secondaryResetAt),
            accountUsage,
            capturedAt ?? CapturedAt);
    }

    private static CodexRateLimitsSnapshot CreateRateLimitsSnapshot(
        int? primaryUsedPercent = 42,
        int? secondaryUsedPercent = 21,
        DateTimeOffset? primaryResetAt = null,
        DateTimeOffset? secondaryResetAt = null) =>
        new(
            new CodexRateLimitWindow(primaryUsedPercent, primaryUsedPercent.HasValue ? 300 : null, primaryResetAt),
            new CodexRateLimitWindow(secondaryUsedPercent, secondaryUsedPercent.HasValue ? 10_080 : null, secondaryResetAt),
            null);

    private static CodexAccountUsageSnapshot CreateAccountUsageSnapshot(
        IReadOnlyList<CodexDailyTokenUsage>? dailyUsageBuckets = null) =>
        new(dailyUsageBuckets);

    private static CodexDailyTokenUsage Bucket(int year, int month, int day, long tokens) =>
        new(new DateOnly(year, month, day), tokens);
}
