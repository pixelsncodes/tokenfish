namespace TokenFish.Providers.Codex.RateLimits;

public sealed record CodexRateLimitsSnapshot
{
    public CodexRateLimitWindow Primary { get; }

    public CodexRateLimitWindow Secondary { get; }

    public string? RateLimitReachedType { get; }

    public CodexRateLimitsSnapshot(
        CodexRateLimitWindow primary,
        CodexRateLimitWindow secondary,
        string? rateLimitReachedType)
    {
        Primary = primary ?? throw new ArgumentNullException(nameof(primary));
        Secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
        RateLimitReachedType = rateLimitReachedType;
    }
}
