namespace TokenFish.Core.Models;

/// <summary>
/// Timestamps are normalized to UTC at construction with DateTimeOffset.ToUniversalTime().
/// </summary>
public sealed record ProviderUsageSnapshot
{
    public ProviderKind Provider { get; }

    public ProviderConnectionState ConnectionState { get; }

    public PercentageUsageMetric UsageWindow { get; }

    public DateTimeOffset? UsageWindowResetAt { get; }

    public TokenCountMetric SessionTokens { get; }

    public TokenCountMetric WeeklyTokens { get; }

    public IReadOnlyList<NormalizedQuotaWindow> QuotaWindows { get; }

    public IReadOnlyList<NormalizedActivityMetric> ActivityMetrics { get; }
    public IReadOnlyList<DailyTokenActivity> DailyActivity { get; }

    public DateTimeOffset CapturedAt { get; }

    /// <summary>
    /// The time at which the provider observed the source data, when that time is
    /// authoritative for runtime freshness. A collection's <see cref="CapturedAt"/>
    /// remains independent and may represent metric-level capture timing.
    /// </summary>
    public DateTimeOffset? SourceObservedAt { get; }

    public ProviderUsageSnapshot(
        ProviderKind provider,
        ProviderConnectionState connectionState,
        PercentageUsageMetric usageWindow,
        DateTimeOffset? usageWindowResetAt,
        TokenCountMetric sessionTokens,
        TokenCountMetric weeklyTokens,
        DateTimeOffset capturedAt,
        IReadOnlyList<NormalizedQuotaWindow>? quotaWindows = null,
        IReadOnlyList<NormalizedActivityMetric>? activityMetrics = null,
        DateTimeOffset? sourceObservedAt = null,
        IReadOnlyList<DailyTokenActivity>? dailyActivity = null)
    {
        Provider = provider;
        ConnectionState = connectionState;
        UsageWindow = usageWindow ?? throw new ArgumentNullException(nameof(usageWindow));
        UsageWindowResetAt = usageWindowResetAt?.ToUniversalTime();
        SessionTokens = sessionTokens ?? throw new ArgumentNullException(nameof(sessionTokens));
        WeeklyTokens = weeklyTokens ?? throw new ArgumentNullException(nameof(weeklyTokens));
        QuotaWindows = quotaWindows?.ToArray() ?? [];
        ActivityMetrics = activityMetrics?.ToArray() ?? [];
        DailyActivity = dailyActivity?.ToArray() ?? [];
        CapturedAt = capturedAt.ToUniversalTime();
        SourceObservedAt = sourceObservedAt?.ToUniversalTime();
    }
}

public sealed record DailyTokenActivity(DateOnly Date, long Tokens);
