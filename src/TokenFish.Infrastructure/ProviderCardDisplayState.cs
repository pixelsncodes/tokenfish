using TokenFish.Core.Models;

namespace TokenFish.Infrastructure;

public sealed record ProviderCardDisplayState
{
    public ProviderKind Provider { get; }

    public string ProviderName { get; }

    public string ConnectionState { get; }

    public IReadOnlyList<PopupQuotaWindowDisplayState> QuotaWindows { get; }

    public IReadOnlyList<PopupActivityDisplayState> ActivityRows { get; }

    public string? EmptyUsageMessage { get; }

    public string FooterText { get; }

    public bool IsStale { get; }
    public IReadOnlyList<DailyTokenActivity> DailyActivity { get; }

    public ProviderCardDisplayState(
        ProviderKind provider,
        string providerName,
        string connectionState,
        IReadOnlyList<PopupQuotaWindowDisplayState>? quotaWindows = null,
        IReadOnlyList<PopupActivityDisplayState>? activityRows = null,
        string? emptyUsageMessage = null,
        string? footerText = null,
        bool isStale = false,
        IReadOnlyList<DailyTokenActivity>? dailyActivity = null)
    {
        Provider = provider;
        ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
        ConnectionState = connectionState ?? throw new ArgumentNullException(nameof(connectionState));
        QuotaWindows = quotaWindows?.ToArray() ?? [];
        ActivityRows = activityRows?.ToArray() ?? [];
        EmptyUsageMessage = emptyUsageMessage;
        FooterText = footerText ?? string.Empty;
        IsStale = isStale;
        DailyActivity=dailyActivity?.ToArray() ?? [];
    }
}
