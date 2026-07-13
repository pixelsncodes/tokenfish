namespace TokenFish.Providers.Claude;

internal sealed record ClaudeBridgeQuotaWindowObservation
{
    public ClaudeBridgeQuotaWindowObservation(
        decimal usedPercentage,
        DateTimeOffset? resetAt,
        DateTimeOffset observedAt)
    {
        if (usedPercentage is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usedPercentage),
                "Used percentage must be between 0 and 100 inclusive.");
        }

        UsedPercentage = usedPercentage;
        ResetAt = resetAt?.ToUniversalTime();
        ObservedAt = observedAt.ToUniversalTime();
    }

    public decimal UsedPercentage { get; }

    public DateTimeOffset? ResetAt { get; }

    public DateTimeOffset ObservedAt { get; }
}
