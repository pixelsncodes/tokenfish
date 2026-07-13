namespace TokenFish.Providers.Claude;

internal sealed record ClaudeStatusLineUsageSnapshot(
    ClaudeStatusLineQuotaWindowObservation? FiveHour,
    ClaudeStatusLineQuotaWindowObservation? SevenDay)
{
    public bool HasUsageData =>
        FiveHour is { HasUsageData: true } ||
        SevenDay is { HasUsageData: true };

    public static ClaudeStatusLineUsageSnapshot Empty { get; } = new(null, null);
}
