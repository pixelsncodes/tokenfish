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
        Assert.Equal(73m, Assert.Single(snapshot.QuotaWindows).UsedPercentage);
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
        Assert.Equal(resetAt, Assert.Single(snapshot.QuotaWindows).ResetAt);
    }

    [Fact]
    public void SecondaryResetTimestampDoesNotReplaceMissingPrimaryResetTimestamp()
    {
        var secondaryResetAt = new DateTimeOffset(2026, 7, 19, 16, 0, 0, TimeSpan.Zero);

        var snapshot = CreateSnapshot(primaryUsedPercent: null, secondaryUsedPercent: 21, secondaryResetAt: secondaryResetAt);

        Assert.Null(snapshot.UsageWindowResetAt);
    }

    [Fact]
    public void SecondaryPercentageDoesNotReplaceOrOverridePrimary()
    {
        var snapshot = CreateSnapshot(primaryUsedPercent: 12, secondaryUsedPercent: 99);

        Assert.Equal(12m, snapshot.UsageWindow.PercentageConsumed);
        Assert.Equal(
            [12m, 99m],
            snapshot.QuotaWindows.Select(window => window.UsedPercentage));
    }

    [Fact]
    public void MissingPrimaryPercentageProducesUnavailableUsageWindowMetric()
    {
        var snapshot = CreateSnapshot(primaryUsedPercent: null);

        Assert.False(snapshot.UsageWindow.IsAvailable);
        Assert.Null(snapshot.UsageWindow.PercentageConsumed);
        Assert.Equal(DataAuthority.LocalProviderReported, snapshot.UsageWindow.Authority);
        Assert.Equal(DataFreshness.Live, snapshot.UsageWindow.Freshness);
        Assert.DoesNotContain(snapshot.QuotaWindows, window => window.WindowId.EndsWith(":primary"));
    }

    [Fact]
    public void MultipleCodexWindowsSurviveNormalizationIndependently()
    {
        var primaryResetAt = new DateTimeOffset(2026, 7, 12, 16, 0, 0, TimeSpan.Zero);
        var secondaryResetAt = new DateTimeOffset(2026, 7, 19, 16, 0, 0, TimeSpan.Zero);

        var snapshot = CreateSnapshot(
            primaryUsedPercent: 14,
            secondaryUsedPercent: 65,
            primaryResetAt: primaryResetAt,
            secondaryResetAt: secondaryResetAt);

        Assert.Collection(
            snapshot.QuotaWindows,
            primary =>
            {
                Assert.Equal("codex:default:primary", primary.WindowId);
                Assert.Equal(14m, primary.UsedPercentage);
                Assert.Equal(primaryResetAt, primary.ResetAt);
                Assert.Equal(TimeSpan.FromMinutes(300), primary.WindowDuration);
            },
            secondary =>
            {
                Assert.Equal("codex:default:secondary", secondary.WindowId);
                Assert.Equal(65m, secondary.UsedPercentage);
                Assert.Equal(secondaryResetAt, secondary.ResetAt);
                Assert.Equal(TimeSpan.FromMinutes(10_080), secondary.WindowDuration);
            });
    }

    [Fact]
    public void RateLimitsByLimitIdGenerateStableProviderScopedIds()
    {
        var snapshot = CreateSnapshot(
            rateLimitsByLimitId:
            [
                new CodexRateLimitBucket(
                    "codex",
                    null,
                    new CodexRateLimitWindow(10, 10_080, null),
                    CodexRateLimitWindow.Unavailable,
                    null),
                new CodexRateLimitBucket(
                    "Codex Other",
                    null,
                    new CodexRateLimitWindow(20, 300, null),
                    CodexRateLimitWindow.Unavailable,
                    null)
            ]);

        Assert.Equal(
            ["codex:codex:primary", "codex:codex-other:primary"],
            snapshot.QuotaWindows.Select(window => window.WindowId));
    }

    [Fact]
    public void KnownExplicitDurationsReceiveCodexSpecificLabels()
    {
        var snapshot = CreateSnapshot(primaryWindowDurationMins: 10_080);

        var window = Assert.Single(snapshot.QuotaWindows);
        Assert.Equal("Weekly", window.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.TokenFishDurationMapping, window.LabelOrigin);
    }

    [Fact]
    public void UnknownDurationRemainsUnlabeled()
    {
        var snapshot = CreateSnapshot(primaryWindowDurationMins: 12_345);

        var window = Assert.Single(snapshot.QuotaWindows);
        Assert.Null(window.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.Unknown, window.LabelOrigin);
    }

    [Fact]
    public void ResetCountdownLengthDoesNotAffectLabel()
    {
        var snapshot = CreateSnapshot(
            primaryWindowDurationMins: 12_345,
            primaryResetAt: CapturedAt.AddDays(7));

        var window = Assert.Single(snapshot.QuotaWindows);
        Assert.Null(window.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.Unknown, window.LabelOrigin);
    }

    [Fact]
    public void CurrentSessionTokensAreUnavailable()
    {
        var snapshot = CreateSnapshot();

        Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.Null(snapshot.SessionTokens.TokenCount);
        Assert.DoesNotContain(snapshot.ActivityMetrics, metric =>
            metric.MetricId.Contains("session", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(snapshot.QuotaWindows, window =>
            window.WindowId.Contains("session", StringComparison.OrdinalIgnoreCase));
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
        var metric = Assert.Single(snapshot.ActivityMetrics);
        Assert.Equal("codex:activity:latest-seven-utc-dates:tokens", metric.MetricId);
        Assert.Equal(UsageActivityUnit.Tokens, metric.Unit);
        Assert.Equal(280, metric.Value);
        Assert.Equal(new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero), metric.IntervalStart);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero), metric.IntervalEnd);
    }

    [Fact]
    public void SevenDayTokenTotalIsLatestSevenUtcDatesNotCalendarWeek()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 15, 15, 30, 0, TimeSpan.Zero);

        var snapshot = CreateSnapshot(
            capturedAt: capturedAt,
            dailyUsageBuckets:
            [
                Bucket(2026, 7, 8, 999),
                Bucket(2026, 7, 9, 10),
                Bucket(2026, 7, 10, 20),
                Bucket(2026, 7, 11, 30),
                Bucket(2026, 7, 12, 40),
                Bucket(2026, 7, 13, 50),
                Bucket(2026, 7, 14, 60),
                Bucket(2026, 7, 15, 70)
            ]);

        Assert.Equal(280, snapshot.WeeklyTokens.TokenCount);
    }

    [Fact]
    public void TokenActivityDoesNotPopulateQuotaFields()
    {
        var snapshot = CreateSnapshot(
            primaryUsedPercent: null,
            dailyUsageBuckets: [Bucket(2026, 7, 12, 123)]);

        Assert.False(snapshot.UsageWindow.IsAvailable);
        Assert.Null(snapshot.UsageWindowResetAt);
        Assert.True(snapshot.WeeklyTokens.IsAvailable);
        Assert.Equal(123, snapshot.WeeklyTokens.TokenCount);
        Assert.Single(snapshot.ActivityMetrics);
        Assert.Empty(snapshot.QuotaWindows);
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
        var metric = Assert.Single(snapshot.ActivityMetrics);
        Assert.False(metric.IsAvailable);
        Assert.Equal(UsageActivityUnit.Tokens, metric.Unit);
    }

    [Fact]
    public void EmptyAvailableBucketsProduceWeeklyTotalZero()
    {
        var snapshot = CreateSnapshot(dailyUsageBuckets: []);

        Assert.True(snapshot.WeeklyTokens.IsAvailable);
        Assert.Equal(0, snapshot.WeeklyTokens.TokenCount);
        var metric = Assert.Single(snapshot.ActivityMetrics);
        Assert.True(metric.IsAvailable);
        Assert.Equal(0, metric.Value);
    }

    [Fact]
    public void WeeklyAuthorityIsLocallyCalculated()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal(DataAuthority.TokenFishDerived, snapshot.WeeklyTokens.Authority);
        Assert.Equal(DataFreshness.Live, snapshot.WeeklyTokens.Freshness);
        var metric = Assert.Single(snapshot.ActivityMetrics);
        Assert.Equal(DataAuthority.TokenFishDerived, metric.Authority);
        Assert.Equal(DataFreshness.Live, metric.Freshness);
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
        Assert.All(snapshot.QuotaWindows, window => Assert.Equal(capturedAt.ToUniversalTime(), window.CapturedAt));
        Assert.All(snapshot.ActivityMetrics, metric => Assert.Equal(capturedAt.ToUniversalTime(), metric.CapturedAt));
    }

    [Fact]
    public void ProviderNativeTypesDoNotLeakIntoAppFacingSnapshotContract()
    {
        var providerNativeNamespace = typeof(CodexRateLimitWindow).Namespace!;

        var leakedProperties = typeof(ProviderUsageSnapshot)
            .GetProperties()
            .Where(property => ContainsProviderNativeType(property.PropertyType, providerNativeNamespace))
            .Select(property => property.Name);

        Assert.Empty(leakedProperties);
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
        int? secondaryUsedPercent = null,
        long? primaryWindowDurationMins = null,
        long? secondaryWindowDurationMins = null,
        DateTimeOffset? primaryResetAt = null,
        DateTimeOffset? secondaryResetAt = null,
        IReadOnlyList<CodexDailyTokenUsage>? dailyUsageBuckets = null,
        bool dailyUsageBucketsAvailable = true,
        DateTimeOffset? capturedAt = null,
        IReadOnlyList<CodexRateLimitBucket>? rateLimitsByLimitId = null)
    {
        var accountUsage = dailyUsageBuckets is null && dailyUsageBucketsAvailable
            ? CreateAccountUsageSnapshot([Bucket(2026, 7, 12, 100)])
            : CreateAccountUsageSnapshot(dailyUsageBuckets);

        return _factory.Create(
            CreateRateLimitsSnapshot(
                primaryUsedPercent,
                secondaryUsedPercent,
                primaryWindowDurationMins,
                secondaryWindowDurationMins,
                primaryResetAt,
                secondaryResetAt,
                rateLimitsByLimitId),
            accountUsage,
            capturedAt ?? CapturedAt);
    }

    private static CodexRateLimitsSnapshot CreateRateLimitsSnapshot(
        int? primaryUsedPercent = 42,
        int? secondaryUsedPercent = null,
        long? primaryWindowDurationMins = null,
        long? secondaryWindowDurationMins = null,
        DateTimeOffset? primaryResetAt = null,
        DateTimeOffset? secondaryResetAt = null,
        IReadOnlyList<CodexRateLimitBucket>? rateLimitsByLimitId = null) =>
        new(
            null,
            null,
            new CodexRateLimitWindow(primaryUsedPercent, primaryUsedPercent.HasValue ? primaryWindowDurationMins ?? 300 : null, primaryResetAt),
            new CodexRateLimitWindow(secondaryUsedPercent, secondaryUsedPercent.HasValue ? secondaryWindowDurationMins ?? 10_080 : null, secondaryResetAt),
            null,
            rateLimitsByLimitId ?? []);

    private static CodexAccountUsageSnapshot CreateAccountUsageSnapshot(
        IReadOnlyList<CodexDailyTokenUsage>? dailyUsageBuckets = null) =>
        new(dailyUsageBuckets);

    private static CodexDailyTokenUsage Bucket(int year, int month, int day, long tokens) =>
        new(new DateOnly(year, month, day), tokens);

    private static bool ContainsProviderNativeType(Type type, string providerNativeNamespace)
    {
        if (type.Namespace?.StartsWith(providerNativeNamespace, StringComparison.Ordinal) == true)
        {
            return true;
        }

        return type.GenericTypeArguments.Any(argument =>
            ContainsProviderNativeType(argument, providerNativeNamespace));
    }
}
