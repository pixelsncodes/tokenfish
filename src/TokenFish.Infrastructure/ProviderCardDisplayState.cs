using TokenFish.Core.Models;

namespace TokenFish.Infrastructure;

public sealed record ProviderCardDisplayState(
    ProviderKind Provider,
    string ProviderName,
    string ConnectionState,
    string Freshness,
    decimal? UsagePercentage,
    string UsageWindow,
    string Reset,
    string SessionTokens,
    string WeeklyTokens,
    string DataAuthority);
