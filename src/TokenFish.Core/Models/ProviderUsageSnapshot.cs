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

    public DateTimeOffset CapturedAt { get; }

    public ProviderUsageSnapshot(
        ProviderKind provider,
        ProviderConnectionState connectionState,
        PercentageUsageMetric usageWindow,
        DateTimeOffset? usageWindowResetAt,
        TokenCountMetric sessionTokens,
        TokenCountMetric weeklyTokens,
        DateTimeOffset capturedAt)
    {
        Provider = provider;
        ConnectionState = connectionState;
        UsageWindow = usageWindow ?? throw new ArgumentNullException(nameof(usageWindow));
        UsageWindowResetAt = usageWindowResetAt?.ToUniversalTime();
        SessionTokens = sessionTokens ?? throw new ArgumentNullException(nameof(sessionTokens));
        WeeklyTokens = weeklyTokens ?? throw new ArgumentNullException(nameof(weeklyTokens));
        CapturedAt = capturedAt.ToUniversalTime();
    }
}
