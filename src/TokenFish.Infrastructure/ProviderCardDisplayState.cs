using TokenFish.Core.Models;

namespace TokenFish.Infrastructure;

public sealed record ProviderCardDisplayState
{
    public ProviderKind Provider { get; }

    public string ProviderName { get; }

    public string ConnectionState { get; }

    public string Freshness { get; }

    public decimal? UsagePercentage { get; }

    public string UsageWindow { get; }

    public string Reset { get; }

    public string SessionTokens { get; }

    public string WeeklyTokens { get; }

    public string DataAuthority { get; }

    public IReadOnlyList<PopupQuotaWindowDisplayState> QuotaWindows { get; }

    public IReadOnlyList<PopupActivityDisplayState> ActivityRows { get; }

    public string? EmptyUsageMessage { get; }

    public string FooterText { get; }

    public bool IsStale { get; }

    public ProviderCardDisplayState(
        ProviderKind provider,
        string providerName,
        string connectionState,
        string freshness,
        decimal? usagePercentage,
        string usageWindow,
        string reset,
        string sessionTokens,
        string weeklyTokens,
        string dataAuthority,
        IReadOnlyList<PopupQuotaWindowDisplayState>? quotaWindows = null,
        IReadOnlyList<PopupActivityDisplayState>? activityRows = null,
        string? emptyUsageMessage = null,
        string? footerText = null,
        bool isStale = false)
    {
        Provider = provider;
        ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
        ConnectionState = connectionState ?? throw new ArgumentNullException(nameof(connectionState));
        Freshness = freshness ?? throw new ArgumentNullException(nameof(freshness));
        UsagePercentage = usagePercentage;
        UsageWindow = usageWindow ?? throw new ArgumentNullException(nameof(usageWindow));
        Reset = reset ?? throw new ArgumentNullException(nameof(reset));
        SessionTokens = sessionTokens ?? throw new ArgumentNullException(nameof(sessionTokens));
        WeeklyTokens = weeklyTokens ?? throw new ArgumentNullException(nameof(weeklyTokens));
        DataAuthority = dataAuthority ?? throw new ArgumentNullException(nameof(dataAuthority));
        QuotaWindows = quotaWindows?.ToArray() ?? [];
        ActivityRows = activityRows?.ToArray() ?? [];
        EmptyUsageMessage = emptyUsageMessage;
        FooterText = footerText ?? string.Empty;
        IsStale = isStale;
    }
}
