namespace TokenFish.Core.Models;

public sealed record PercentageUsageMetric
{
    public decimal? PercentageConsumed { get; }

    public DataAuthority Authority { get; }

    public DataFreshness Freshness { get; }

    public bool IsAvailable => PercentageConsumed.HasValue;

    public PercentageUsageMetric(
        decimal? percentageConsumed,
        DataAuthority authority,
        DataFreshness freshness)
    {
        if (percentageConsumed is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentageConsumed),
                "Percentage must be between 0 and 100 inclusive.");
        }

        PercentageConsumed = percentageConsumed;
        Authority = authority;
        Freshness = freshness;
    }

    public static PercentageUsageMetric Unavailable(
        DataAuthority authority,
        DataFreshness freshness) =>
        new(null, authority, freshness);
}
