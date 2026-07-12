using TokenFish.Core.Models;

namespace TokenFish.Core.Tests;

public sealed class NormalizedUsageContractTests
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 7, 12, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ValidQuotaWindowCapturesProviderNeutralFields()
    {
        var resetAt = new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero);

        var window = new NormalizedQuotaWindow(
            ProviderKind.Codex,
            "codex:default:primary",
            "Weekly",
            UsageMetricLabelOrigin.TokenFishDurationMapping,
            14m,
            resetAt,
            TimeSpan.FromDays(7),
            UsageMetricAvailability.Available,
            CapturedAt,
            DataAuthority.LocalProviderReported,
            DataFreshness.Live,
            "account/rateLimits/read");

        Assert.Equal(ProviderKind.Codex, window.Provider);
        Assert.Equal("codex:default:primary", window.WindowId);
        Assert.Equal("Weekly", window.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.TokenFishDurationMapping, window.LabelOrigin);
        Assert.Equal(14m, window.UsedPercentage);
        Assert.Equal(resetAt, window.ResetAt);
        Assert.Equal(TimeSpan.FromDays(7), window.WindowDuration);
        Assert.Equal(UsageMetricAvailability.Available, window.Availability);
        Assert.True(window.IsAvailable);
        Assert.Equal(DataAuthority.LocalProviderReported, window.Authority);
        Assert.Equal(DataFreshness.Live, window.Freshness);
        Assert.Equal("account/rateLimits/read", window.Source);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100")]
    public void QuotaWindowAcceptsBoundaryPercentages(string percentageText)
    {
        var window = CreateQuotaWindow(usedPercentage: decimal.Parse(percentageText));

        Assert.Equal(decimal.Parse(percentageText), window.UsedPercentage);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    public void QuotaWindowRejectsInvalidPercentages(string percentageText)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateQuotaWindow(usedPercentage: decimal.Parse(percentageText)));
    }

    [Fact]
    public void QuotaWindowRejectsNonPositiveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateQuotaWindow(windowDuration: TimeSpan.Zero));
    }

    [Fact]
    public void QuotaWindowAllowsMissingReset()
    {
        var window = new NormalizedQuotaWindow(
            ProviderKind.Codex,
            "codex:default:primary",
            "5h",
            UsageMetricLabelOrigin.TokenFishDurationMapping,
            50m,
            null,
            TimeSpan.FromHours(5),
            UsageMetricAvailability.Available,
            CapturedAt,
            DataAuthority.LocalProviderReported,
            DataFreshness.Live,
            "account/rateLimits/read");

        Assert.Null(window.ResetAt);
    }

    [Fact]
    public void QuotaWindowAllowsProviderSuppliedLabel()
    {
        var window = CreateQuotaWindow(
            displayLabel: "Synthetic provider label",
            labelOrigin: UsageMetricLabelOrigin.ProviderSupplied);

        Assert.Equal("Synthetic provider label", window.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.ProviderSupplied, window.LabelOrigin);
    }

    [Fact]
    public void QuotaWindowUnknownLabelOriginRequiresNullLabel()
    {
        var window = CreateQuotaWindow(
            displayLabel: null,
            labelOrigin: UsageMetricLabelOrigin.Unknown);

        Assert.Null(window.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.Unknown, window.LabelOrigin);
    }

    [Fact]
    public void QuotaWindowRejectsUnknownLabelWithText()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateQuotaWindow(
                displayLabel: "Derived from reset countdown",
                labelOrigin: UsageMetricLabelOrigin.Unknown));
    }

    [Fact]
    public void UnavailableQuotaWindowCarriesAvailabilityWithoutValue()
    {
        var window = NormalizedQuotaWindow.Unavailable(
            ProviderKind.Codex,
            "codex:default:primary",
            CapturedAt,
            DataAuthority.LocalProviderReported,
            DataFreshness.Unknown,
            "account/rateLimits/read");

        Assert.False(window.IsAvailable);
        Assert.Null(window.UsedPercentage);
        Assert.Null(window.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.Unknown, window.LabelOrigin);
    }

    [Fact]
    public void AvailableQuotaWindowRequiresPercentage()
    {
        Assert.Throws<ArgumentException>(() =>
            new NormalizedQuotaWindow(
                ProviderKind.Codex,
                "codex:default:primary",
                null,
                UsageMetricLabelOrigin.Unknown,
                null,
                null,
                null,
                UsageMetricAvailability.Available,
                CapturedAt,
                DataAuthority.LocalProviderReported,
                DataFreshness.Live,
                "account/rateLimits/read"));
    }

    [Fact]
    public void ValidActivityMetricCapturesTokenInterval()
    {
        var intervalStart = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
        var intervalEnd = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero);

        var metric = CreateActivityMetric(
            value: 123,
            intervalStart: intervalStart,
            intervalEnd: intervalEnd);

        Assert.Equal(ProviderKind.Codex, metric.Provider);
        Assert.Equal("codex:activity:latest-seven-days:tokens", metric.MetricId);
        Assert.Equal("Latest 7 days", metric.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.TokenFishDurationMapping, metric.LabelOrigin);
        Assert.Equal(123, metric.Value);
        Assert.Equal(UsageActivityUnit.Tokens, metric.Unit);
        Assert.Equal(intervalStart, metric.IntervalStart);
        Assert.Equal(intervalEnd, metric.IntervalEnd);
        Assert.True(metric.IsAvailable);
        Assert.Equal("account/usage/read", metric.Source);
    }

    [Fact]
    public void ActivityMetricRejectsNegativeTokenCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateActivityMetric(value: -1));
    }

    [Fact]
    public void ActivityMetricRejectsInvalidInterval()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateActivityMetric(
                intervalStart: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
                intervalEnd: new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void ActivityMetricRequiresCompleteIntervalWhenKnown()
    {
        Assert.Throws<ArgumentException>(() =>
            new NormalizedActivityMetric(
                ProviderKind.Codex,
                "codex:activity:latest-seven-days:tokens",
                "Latest 7 days",
                UsageMetricLabelOrigin.TokenFishDurationMapping,
                100,
                UsageActivityUnit.Tokens,
                new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                null,
                UsageMetricAvailability.Available,
                CapturedAt,
                DataAuthority.TokenFishDerived,
                DataFreshness.Live,
                "account/usage/read"));
    }

    [Fact]
    public void ActivityMetricCanBeUnavailable()
    {
        var metric = NormalizedActivityMetric.Unavailable(
            ProviderKind.Codex,
            "codex:activity:latest-seven-days:tokens",
            UsageActivityUnit.Tokens,
            CapturedAt,
            DataAuthority.TokenFishDerived,
            DataFreshness.Unknown,
            "account/usage/read");

        Assert.False(metric.IsAvailable);
        Assert.Null(metric.Value);
        Assert.Equal(UsageActivityUnit.Tokens, metric.Unit);
        Assert.Null(metric.IntervalStart);
        Assert.Null(metric.IntervalEnd);
    }

    [Fact]
    public void AvailableActivityMetricRequiresValue()
    {
        Assert.Throws<ArgumentException>(() =>
            new NormalizedActivityMetric(
                ProviderKind.Codex,
                "codex:activity:latest-seven-days:tokens",
                null,
                UsageMetricLabelOrigin.Unknown,
                null,
                UsageActivityUnit.Tokens,
                null,
                null,
                UsageMetricAvailability.Available,
                CapturedAt,
                DataAuthority.TokenFishDerived,
                DataFreshness.Live,
                "account/usage/read"));
    }

    [Fact]
    public void ActivityMetricUnknownLabelOriginRequiresNullLabel()
    {
        var metric = CreateActivityMetric(
            displayLabel: null,
            labelOrigin: UsageMetricLabelOrigin.Unknown);

        Assert.Null(metric.DisplayLabel);
        Assert.Equal(UsageMetricLabelOrigin.Unknown, metric.LabelOrigin);
    }

    [Fact]
    public void TimestampsAreNormalizedToUtc()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 12, 1, 30, 0, TimeSpan.FromHours(-7));
        var resetAt = new DateTimeOffset(2026, 7, 12, 2, 30, 0, TimeSpan.FromHours(-7));
        var intervalStart = new DateTimeOffset(2026, 7, 6, 17, 0, 0, TimeSpan.FromHours(-7));
        var intervalEnd = new DateTimeOffset(2026, 7, 13, 17, 0, 0, TimeSpan.FromHours(-7));

        var window = CreateQuotaWindow(resetAt: resetAt, capturedAt: capturedAt);
        var metric = CreateActivityMetric(
            intervalStart: intervalStart,
            intervalEnd: intervalEnd,
            capturedAt: capturedAt);

        Assert.Equal(TimeSpan.Zero, window.ResetAt?.Offset);
        Assert.Equal(TimeSpan.Zero, window.CapturedAt.Offset);
        Assert.Equal(resetAt.ToUniversalTime(), window.ResetAt);
        Assert.Equal(capturedAt.ToUniversalTime(), window.CapturedAt);
        Assert.Equal(TimeSpan.Zero, metric.IntervalStart?.Offset);
        Assert.Equal(TimeSpan.Zero, metric.IntervalEnd?.Offset);
        Assert.Equal(TimeSpan.Zero, metric.CapturedAt.Offset);
    }

    [Fact]
    public void NormalizedRecordsHaveValueEquality()
    {
        var first = CreateQuotaWindow();
        var second = CreateQuotaWindow();

        Assert.Equal(first, second);
        Assert.NotSame(first, second);
    }

    private static NormalizedQuotaWindow CreateQuotaWindow(
        decimal usedPercentage = 50m,
        string? displayLabel = "5h",
        UsageMetricLabelOrigin labelOrigin = UsageMetricLabelOrigin.TokenFishDurationMapping,
        DateTimeOffset? resetAt = null,
        TimeSpan? windowDuration = null,
        DateTimeOffset? capturedAt = null) =>
        new(
            ProviderKind.Codex,
            "codex:default:primary",
            displayLabel,
            labelOrigin,
            usedPercentage,
            resetAt ?? new DateTimeOffset(2026, 7, 12, 9, 30, 0, TimeSpan.Zero),
            windowDuration ?? TimeSpan.FromHours(5),
            UsageMetricAvailability.Available,
            capturedAt ?? CapturedAt,
            DataAuthority.LocalProviderReported,
            DataFreshness.Live,
            "account/rateLimits/read");

    private static NormalizedActivityMetric CreateActivityMetric(
        long value = 100,
        string? displayLabel = "Latest 7 days",
        UsageMetricLabelOrigin labelOrigin = UsageMetricLabelOrigin.TokenFishDurationMapping,
        DateTimeOffset? intervalStart = null,
        DateTimeOffset? intervalEnd = null,
        DateTimeOffset? capturedAt = null) =>
        new(
            ProviderKind.Codex,
            "codex:activity:latest-seven-days:tokens",
            displayLabel,
            labelOrigin,
            value,
            UsageActivityUnit.Tokens,
            intervalStart ?? new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
            intervalEnd ?? new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            UsageMetricAvailability.Available,
            capturedAt ?? CapturedAt,
            DataAuthority.TokenFishDerived,
            DataFreshness.Live,
            "account/usage/read");
}
