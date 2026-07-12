namespace TokenFish.Core.Models;

public sealed record TokenCountMetric
{
    public long? TokenCount { get; }

    public DataAuthority Authority { get; }

    public DataFreshness Freshness { get; }

    public bool IsAvailable => TokenCount.HasValue;

    public TokenCountMetric(
        long? tokenCount,
        DataAuthority authority,
        DataFreshness freshness)
    {
        if (tokenCount is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokenCount),
                "Token count must be zero or greater.");
        }

        TokenCount = tokenCount;
        Authority = authority;
        Freshness = freshness;
    }

    public static TokenCountMetric Unavailable(
        DataAuthority authority,
        DataFreshness freshness) =>
        new(null, authority, freshness);
}
