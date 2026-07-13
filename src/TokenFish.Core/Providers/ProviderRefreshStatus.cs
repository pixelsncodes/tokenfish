namespace TokenFish.Core.Providers;

public sealed record ProviderRefreshStatus(
    bool IsRefreshActive,
    DateTimeOffset? LatestAttemptedAtUtc,
    DateTimeOffset? LatestSucceededAtUtc,
    ProviderRefreshOutcome Outcome,
    long Version)
{
    public static ProviderRefreshStatus Initial { get; } = new(
        IsRefreshActive: false,
        LatestAttemptedAtUtc: null,
        LatestSucceededAtUtc: null,
        ProviderRefreshOutcome.NeverRefreshed,
        Version: 0);
}
