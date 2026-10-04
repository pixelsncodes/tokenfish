using TokenFish.Core.Models;
using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex;

public sealed class CodexUsageSnapshotFactory
{
    private const string RateLimitsSource = "account/rateLimits/read";
    private const string AccountUsageSource = "account/usage/read";
    private const string LatestSevenUtcDatesMetricId = "codex:activity:latest-seven-utc-dates:tokens";

    public ProviderUsageSnapshot Create(
        CodexRateLimitsSnapshot rateLimits,
        CodexAccountUsageSnapshot accountUsage,
        DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(rateLimits);
        ArgumentNullException.ThrowIfNull(accountUsage);

        var quotaWindows = CreateQuotaWindows(rateLimits, capturedAt);
        var activityMetrics = CreateActivityMetrics(accountUsage, capturedAt).ToList();
        AddSummaryMetrics(activityMetrics,accountUsage,capturedAt);
        var compatibilityUsageWindow = CreateCompatibilityUsageWindow(quotaWindows);
        var compatibilityTokenMetric = CreateCompatibilityTokenMetric(activityMetrics);

        return new ProviderUsageSnapshot(
            ProviderKind.Codex,
            ProviderConnectionState.Connected,
            compatibilityUsageWindow,
            FindCompatibilityQuotaWindow(quotaWindows)?.ResetAt,
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            compatibilityTokenMetric,
            capturedAt,
            quotaWindows,
            activityMetrics,
            dailyActivity: accountUsage.DailyUsageBuckets?.Select(bucket=>new DailyTokenActivity(bucket.StartDate,bucket.Tokens)).ToArray());
    }

    private static IReadOnlyList<NormalizedQuotaWindow> CreateQuotaWindows(
        CodexRateLimitsSnapshot rateLimits,
        DateTimeOffset capturedAt)
    {
        var buckets = rateLimits.RateLimitsByLimitId.Count > 0
            ? rateLimits.RateLimitsByLimitId
            :
            [
                new CodexRateLimitBucket(
                    rateLimits.LimitId,
                    rateLimits.LimitName,
                    rateLimits.Primary,
                    rateLimits.Secondary,
                    rateLimits.RateLimitReachedType)
            ];

        var windows = new List<NormalizedQuotaWindow>();
        foreach (var bucket in buckets)
        {
            var name=buckets.Count>1 ? bucket.LimitName ?? bucket.LimitId : bucket.LimitName;
            AddWindow(windows, bucket.LimitId, "primary", bucket.Primary, capturedAt,name);
            AddWindow(windows, bucket.LimitId, "secondary", bucket.Secondary, capturedAt,name);
        }

        return windows;
    }

    private static void AddWindow(
        List<NormalizedQuotaWindow> windows,
        string? limitId,
        string windowKind,
        CodexRateLimitWindow window,
        DateTimeOffset capturedAt,
        string? bucketName = null)
    {
        if (!window.IsAvailable)
        {
            return;
        }

        var (displayLabel, labelOrigin) = CreateDurationLabel(window.WindowDurationMins);
        if(!string.IsNullOrWhiteSpace(bucketName))
        {
            displayLabel=$"{bucketName.Trim()} · {displayLabel ?? windowKind}";
            labelOrigin=UsageMetricLabelOrigin.ProviderSupplied;
        }
        windows.Add(
            new NormalizedQuotaWindow(
                ProviderKind.Codex,
                CreateQuotaWindowId(limitId, windowKind),
                displayLabel,
                labelOrigin,
                window.UsedPercent,
                window.ResetsAt,
                window.WindowDurationMins.HasValue
                    ? TimeSpan.FromMinutes(window.WindowDurationMins.Value)
                    : null,
                UsageMetricAvailability.Available,
                capturedAt,
                DataAuthority.LocalProviderReported,
                DataFreshness.Live,
                RateLimitsSource));
    }

    private static (string? DisplayLabel, UsageMetricLabelOrigin LabelOrigin) CreateDurationLabel(
        long? windowDurationMins)
    {
        if (!windowDurationMins.HasValue)
        {
            return (null, UsageMetricLabelOrigin.Unknown);
        }

        return IsApproximateWindow(windowDurationMins.Value, 5 * 60) ? ("5h", UsageMetricLabelOrigin.TokenFishDurationMapping) :
            IsApproximateWindow(windowDurationMins.Value, 24 * 60) ? ("Daily", UsageMetricLabelOrigin.TokenFishDurationMapping) :
            IsApproximateWindow(windowDurationMins.Value, 7 * 24 * 60) ? ("Weekly", UsageMetricLabelOrigin.TokenFishDurationMapping) :
            IsApproximateWindow(windowDurationMins.Value, 30 * 24 * 60) ? ("Monthly", UsageMetricLabelOrigin.TokenFishDurationMapping) :
            IsApproximateWindow(windowDurationMins.Value, 365 * 24 * 60) ? ("Annual", UsageMetricLabelOrigin.TokenFishDurationMapping) :
            (null, UsageMetricLabelOrigin.Unknown);
    }

    private static bool IsApproximateWindow(long actualMinutes, long expectedMinutes)
    {
        var lowerBound = expectedMinutes * 0.95;
        var upperBound = expectedMinutes * 1.05;

        return actualMinutes >= lowerBound && actualMinutes <= upperBound;
    }

    private static string CreateQuotaWindowId(string? limitId, string windowKind) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"codex:{SanitizeLimitId(limitId)}:{windowKind}");

    private static string SanitizeLimitId(string? limitId)
    {
        if (string.IsNullOrWhiteSpace(limitId))
        {
            return "default";
        }

        var sanitized = new string(
            limitId
                .Trim()
                .ToLowerInvariant()
                .Select(character =>
                    char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                        ? character
                        : '-')
                .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }

    private static PercentageUsageMetric CreateCompatibilityUsageWindow(
        IReadOnlyList<NormalizedQuotaWindow> quotaWindows)
    {
        var window = FindCompatibilityQuotaWindow(quotaWindows);

        return window is not null
            ? new PercentageUsageMetric(
                window.UsedPercentage,
                window.Authority,
                window.Freshness)
            : PercentageUsageMetric.Unavailable(
                DataAuthority.LocalProviderReported,
                DataFreshness.Live);
    }

    private static NormalizedQuotaWindow? FindCompatibilityQuotaWindow(
        IReadOnlyList<NormalizedQuotaWindow> quotaWindows) =>
        quotaWindows.FirstOrDefault(window =>
            window.IsAvailable &&
            window.WindowId.EndsWith(":primary", StringComparison.Ordinal));

    private static IReadOnlyList<NormalizedActivityMetric> CreateActivityMetrics(
        CodexAccountUsageSnapshot accountUsage,
        DateTimeOffset capturedAt)
    {
        if (!accountUsage.HasDailyUsageBuckets)
        {
            return
            [
                NormalizedActivityMetric.Unavailable(
                    ProviderKind.Codex,
                    LatestSevenUtcDatesMetricId,
                    UsageActivityUnit.Tokens,
                    capturedAt,
                    DataAuthority.TokenFishDerived,
                    DataFreshness.Unknown,
                    AccountUsageSource)
            ];
        }

        var captureDate = DateOnly.FromDateTime(capturedAt.ToUniversalTime().Date);
        var oldestIncludedDate = captureDate.AddDays(-6);
        var intervalStart = new DateTimeOffset(
            oldestIncludedDate.ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);
        var intervalEnd = new DateTimeOffset(
            captureDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        try
        {
            var total = 0L;
            foreach (var bucket in accountUsage.DailyUsageBuckets!)
            {
                if (bucket.StartDate > captureDate)
                {
                    throw new CodexUsageSnapshotNormalizationException(
                        "Codex usage data could not be normalized.");
                }

                if (bucket.StartDate < oldestIncludedDate)
                {
                    continue;
                }

                checked
                {
                    total += bucket.Tokens;
                }
            }

            return
            [
                new NormalizedActivityMetric(
                    ProviderKind.Codex,
                    LatestSevenUtcDatesMetricId,
                    "Latest 7 UTC dates",
                    UsageMetricLabelOrigin.TokenFishDurationMapping,
                    total,
                    UsageActivityUnit.Tokens,
                    intervalStart,
                    intervalEnd,
                    UsageMetricAvailability.Available,
                    capturedAt,
                    DataAuthority.TokenFishDerived,
                    DataFreshness.Live,
                    AccountUsageSource)
            ];
        }
        catch (OverflowException exception)
        {
            throw new CodexUsageSnapshotNormalizationException(
                "Codex usage data could not be normalized.",
                exception);
        }
    }

    private static TokenCountMetric CreateCompatibilityTokenMetric(
        IReadOnlyList<NormalizedActivityMetric> activityMetrics)
    {
        var metric = activityMetrics.FirstOrDefault(metric =>
            metric.MetricId == LatestSevenUtcDatesMetricId &&
            metric.Unit == UsageActivityUnit.Tokens);

        return metric is { IsAvailable: true }
            ? new TokenCountMetric(
                metric.Value,
                metric.Authority,
                metric.Freshness)
            : TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown);
    }

    private static void AddSummaryMetrics(List<NormalizedActivityMetric> metrics,CodexAccountUsageSnapshot usage,DateTimeOffset capturedAt)
    {
        void Add(string id,string label,long? value,UsageActivityUnit unit)
        {
            if(value is null or <0) return;
            metrics.Add(new(ProviderKind.Codex,"codex:summary:"+id,label,UsageMetricLabelOrigin.ProviderSupplied,value,unit,
                null,null,UsageMetricAvailability.Available,capturedAt,DataAuthority.LocalProviderReported,DataFreshness.Live,AccountUsageSource));
        }
        Add("lifetime-tokens","Lifetime tokens",usage.LifetimeTokens,UsageActivityUnit.Tokens);
        Add("peak-daily-tokens","Peak daily tokens",usage.PeakDailyTokens,UsageActivityUnit.Tokens);
        Add("longest-turn","Longest turn",usage.LongestRunningTurnSec,UsageActivityUnit.Seconds);
        Add("current-streak","Current streak",usage.CurrentStreakDays,UsageActivityUnit.Days);
        Add("longest-streak","Longest streak",usage.LongestStreakDays,UsageActivityUnit.Days);
    }
}
