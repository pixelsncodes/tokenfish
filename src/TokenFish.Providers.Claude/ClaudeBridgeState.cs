namespace TokenFish.Providers.Claude;

internal sealed record ClaudeBridgeState(
    ClaudeBridgeQuotaWindowObservation? FiveHour,
    ClaudeBridgeQuotaWindowObservation? SevenDay)
{
    public bool HasUsageData => FiveHour is not null || SevenDay is not null;

    public static ClaudeBridgeState Empty { get; } = new(null, null);

    public static ClaudeBridgeState FromStatusLineUsage(
        ClaudeStatusLineUsageSnapshot usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        return new ClaudeBridgeState(
            FromStatusLineWindow(usage.FiveHour),
            FromStatusLineWindow(usage.SevenDay));
    }

    private static ClaudeBridgeQuotaWindowObservation? FromStatusLineWindow(
        ClaudeStatusLineQuotaWindowObservation? observation)
    {
        if (observation is not { UsedPercentage: { } usedPercentage })
        {
            return null;
        }

        return new ClaudeBridgeQuotaWindowObservation(
            usedPercentage,
            observation.ResetAt,
            observation.ObservedAt);
    }
}
