namespace TokenFish.Providers.Claude;

internal sealed record ClaudeStatusLineQuotaWindowObservation(
    decimal? UsedPercentage,
    DateTimeOffset? ResetAt,
    DateTimeOffset ObservedAt)
{
    public bool HasUsageData => UsedPercentage.HasValue;
}
