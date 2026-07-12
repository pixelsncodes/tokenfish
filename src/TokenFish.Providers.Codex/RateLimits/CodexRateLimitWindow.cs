namespace TokenFish.Providers.Codex.RateLimits;

public sealed record CodexRateLimitWindow
{
    public int? UsedPercent { get; }

    public long? WindowDurationMins { get; }

    public DateTimeOffset? ResetsAt { get; }

    public bool IsAvailable => UsedPercent.HasValue;

    public CodexRateLimitWindow(
        int? usedPercent,
        long? windowDurationMins,
        DateTimeOffset? resetsAt)
    {
        if (usedPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usedPercent),
                "Used percentage must be between 0 and 100 inclusive.");
        }

        if (windowDurationMins is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowDurationMins),
                "Window duration must be greater than zero.");
        }

        UsedPercent = usedPercent;
        WindowDurationMins = windowDurationMins;
        ResetsAt = resetsAt?.ToUniversalTime();
    }

    public static CodexRateLimitWindow Unavailable { get; } = new(null, null, null);
}
