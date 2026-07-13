namespace TokenFish.Infrastructure;

public sealed class PopupDisplayStateUpdatePlanner
{
    private TrayPopupDisplayState? _current;

    public PopupDisplayStateUpdatePlan Plan(TrayPopupDisplayState next)
    {
        ArgumentNullException.ThrowIfNull(next);

        var previous = _current;
        _current = next;

        if (previous is null)
        {
            return PopupDisplayStateUpdatePlan.Rebuild(next, affectsLayout: true);
        }

        var rebuildProviderCards = ProviderStructureChanged(previous.Providers, next.Providers);
        var progressUpdates = rebuildProviderCards
            ? []
            : CreateProgressUpdates(previous.Providers, next.Providers);
        var affectsLayout = rebuildProviderCards || LayoutStateChanged(previous, next);

        return new PopupDisplayStateUpdatePlan(
            rebuildProviderCards,
            affectsLayout,
            progressUpdates);
    }

    private static bool ProviderStructureChanged(
        IReadOnlyList<ProviderCardDisplayState> previous,
        IReadOnlyList<ProviderCardDisplayState> next)
    {
        if (previous.Count != next.Count)
        {
            return true;
        }

        for (var providerIndex = 0; providerIndex < previous.Count; providerIndex++)
        {
            if (ProviderStructureChanged(previous[providerIndex], next[providerIndex]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ProviderStructureChanged(
        ProviderCardDisplayState previous,
        ProviderCardDisplayState next)
    {
        if (previous.Provider != next.Provider ||
            previous.ProviderName != next.ProviderName ||
            previous.QuotaWindows.Count != next.QuotaWindows.Count ||
            previous.ActivityRows.Count != next.ActivityRows.Count ||
            (previous.EmptyUsageMessage is null) != (next.EmptyUsageMessage is null) ||
            string.IsNullOrWhiteSpace(previous.FooterText) != string.IsNullOrWhiteSpace(next.FooterText))
        {
            return true;
        }

        for (var quotaIndex = 0; quotaIndex < previous.QuotaWindows.Count; quotaIndex++)
        {
            var previousQuota = previous.QuotaWindows[quotaIndex];
            var nextQuota = next.QuotaWindows[quotaIndex];
            if (previousQuota.Label != nextQuota.Label)
            {
                return true;
            }
        }

        for (var activityIndex = 0; activityIndex < previous.ActivityRows.Count; activityIndex++)
        {
            var previousActivity = previous.ActivityRows[activityIndex];
            var nextActivity = next.ActivityRows[activityIndex];
            if (previousActivity.Label != nextActivity.Label)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<PopupProgressValueUpdate> CreateProgressUpdates(
        IReadOnlyList<ProviderCardDisplayState> previous,
        IReadOnlyList<ProviderCardDisplayState> next)
    {
        var updates = new List<PopupProgressValueUpdate>();
        for (var providerIndex = 0; providerIndex < next.Count; providerIndex++)
        {
            for (var quotaIndex = 0; quotaIndex < next[providerIndex].QuotaWindows.Count; quotaIndex++)
            {
                var previousValue = previous[providerIndex].QuotaWindows[quotaIndex].ProgressValue;
                var nextValue = next[providerIndex].QuotaWindows[quotaIndex].ProgressValue;
                if (previousValue != nextValue)
                {
                    updates.Add(new PopupProgressValueUpdate(providerIndex, quotaIndex, nextValue));
                }
            }
        }

        return updates;
    }

    private static bool LayoutStateChanged(
        TrayPopupDisplayState previous,
        TrayPopupDisplayState next)
    {
        if (previous.ApplicationState != next.ApplicationState ||
            previous.StatusText != next.StatusText ||
            previous.RefreshCommandState != next.RefreshCommandState)
        {
            return true;
        }

        for (var providerIndex = 0; providerIndex < previous.Providers.Count; providerIndex++)
        {
            var previousProvider = previous.Providers[providerIndex];
            var nextProvider = next.Providers[providerIndex];
            if (previousProvider.ConnectionState != nextProvider.ConnectionState ||
                previousProvider.EmptyUsageMessage != nextProvider.EmptyUsageMessage)
            {
                return true;
            }

            for (var quotaIndex = 0; quotaIndex < previousProvider.QuotaWindows.Count; quotaIndex++)
            {
                var previousQuota = previousProvider.QuotaWindows[quotaIndex];
                var nextQuota = nextProvider.QuotaWindows[quotaIndex];
                if (previousQuota.PercentageText != nextQuota.PercentageText ||
                    previousQuota.RelativeResetText != nextQuota.RelativeResetText ||
                    previousQuota.ExactResetText != nextQuota.ExactResetText)
                {
                    return true;
                }
            }

            for (var activityIndex = 0; activityIndex < previousProvider.ActivityRows.Count; activityIndex++)
            {
                var previousActivity = previousProvider.ActivityRows[activityIndex];
                var nextActivity = nextProvider.ActivityRows[activityIndex];
                if (previousActivity.IntervalText != nextActivity.IntervalText ||
                    previousActivity.ValueText != nextActivity.ValueText)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public sealed record PopupDisplayStateUpdatePlan(
    bool RebuildProviderCards,
    bool AffectsLayout,
    IReadOnlyList<PopupProgressValueUpdate> ProgressValueUpdates)
{
    public static PopupDisplayStateUpdatePlan Rebuild(
        TrayPopupDisplayState state,
        bool affectsLayout)
    {
        _ = state;
        return new PopupDisplayStateUpdatePlan(
            RebuildProviderCards: true,
            AffectsLayout: affectsLayout,
            ProgressValueUpdates: []);
    }
}

public sealed record PopupProgressValueUpdate(
    int ProviderIndex,
    int QuotaWindowIndex,
    decimal ProgressValue);
