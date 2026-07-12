namespace TokenFish.Providers.Codex.Usage;

public sealed record CodexAccountUsageSnapshot
{
    public IReadOnlyList<CodexDailyTokenUsage>? DailyUsageBuckets { get; }

    public bool HasDailyUsageBuckets => DailyUsageBuckets is not null;

    public CodexAccountUsageSnapshot(IReadOnlyList<CodexDailyTokenUsage>? dailyUsageBuckets)
    {
        DailyUsageBuckets = dailyUsageBuckets;
    }
}
