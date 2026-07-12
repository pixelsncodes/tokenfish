namespace TokenFish.Providers.Codex.Usage;

public sealed record CodexDailyTokenUsage
{
    public DateOnly StartDate { get; }

    public long Tokens { get; }

    public CodexDailyTokenUsage(DateOnly startDate, long tokens)
    {
        if (tokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokens), "Token count must be zero or greater.");
        }

        StartDate = startDate;
        Tokens = tokens;
    }
}
