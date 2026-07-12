namespace TokenFish.Providers.Codex.RateLimits;

public sealed record CodexRateLimitBucket
{
    public string? LimitId { get; }

    public string? LimitName { get; }

    public CodexRateLimitWindow Primary { get; }

    public CodexRateLimitWindow Secondary { get; }

    public string? RateLimitReachedType { get; }

    public CodexRateLimitBucket(
        string? limitId,
        string? limitName,
        CodexRateLimitWindow primary,
        CodexRateLimitWindow secondary,
        string? rateLimitReachedType)
    {
        LimitId = string.IsNullOrWhiteSpace(limitId) ? null : limitId;
        LimitName = string.IsNullOrWhiteSpace(limitName) ? null : limitName;
        Primary = primary ?? throw new ArgumentNullException(nameof(primary));
        Secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
        RateLimitReachedType = string.IsNullOrWhiteSpace(rateLimitReachedType)
            ? null
            : rateLimitReachedType;
    }
}
