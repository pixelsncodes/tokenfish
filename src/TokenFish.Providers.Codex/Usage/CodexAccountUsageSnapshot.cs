namespace TokenFish.Providers.Codex.Usage;

public sealed record CodexAccountUsageSnapshot
{
    public IReadOnlyList<CodexDailyTokenUsage>? DailyUsageBuckets { get; }

    public bool HasDailyUsageBuckets => DailyUsageBuckets is not null;
    public long? LifetimeTokens { get; init; }
    public long? PeakDailyTokens { get; init; }
    public long? LongestRunningTurnSec { get; init; }
    public long? CurrentStreakDays { get; init; }
    public long? LongestStreakDays { get; init; }

    public CodexAccountUsageSnapshot(IReadOnlyList<CodexDailyTokenUsage>? dailyUsageBuckets)
    {
        DailyUsageBuckets = dailyUsageBuckets;
    }
}
