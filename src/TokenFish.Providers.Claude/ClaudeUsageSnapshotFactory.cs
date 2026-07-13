using TokenFish.Core.Models;

namespace TokenFish.Providers.Claude;

internal sealed class ClaudeUsageSnapshotFactory
{
    private const string BridgeSource = "claude-status-line-bridge";
    private const string FiveHourWindowId = "claude:status-line:five-hour";
    private const string SevenDayWindowId = "claude:status-line:seven-day";
    private static readonly TimeSpan FiveHourDuration = TimeSpan.FromMinutes(300);
    private static readonly TimeSpan SevenDayDuration = TimeSpan.FromMinutes(10_080);

    public ProviderUsageSnapshot Create(
        ClaudeBridgeState state,
        DateTimeOffset nowUtc,
        TimeSpan freshnessThreshold,
        TimeSpan futureTimestampTolerance)
    {
        ArgumentNullException.ThrowIfNull(state);

        var normalizedNow = nowUtc.ToUniversalTime();
        var quotaWindows = new List<NormalizedQuotaWindow>(2);

        AddWindow(
            quotaWindows,
            state.FiveHour,
            FiveHourWindowId,
            "5h",
            FiveHourDuration,
            normalizedNow,
            freshnessThreshold,
            futureTimestampTolerance);
        AddWindow(
            quotaWindows,
            state.SevenDay,
            SevenDayWindowId,
            "7d",
            SevenDayDuration,
            normalizedNow,
            freshnessThreshold,
            futureTimestampTolerance);

        if (quotaWindows.Count == 0)
        {
            return CreateUnavailable(normalizedNow);
        }

        var capturedAt = quotaWindows.Max(window => window.CapturedAt);
        var compatibilityWindow = quotaWindows.FirstOrDefault(window =>
            StringComparer.Ordinal.Equals(window.WindowId, FiveHourWindowId));

        return new ProviderUsageSnapshot(
            ProviderKind.Claude,
            ProviderConnectionState.Connected,
            compatibilityWindow is not null
                ? new PercentageUsageMetric(
                    compatibilityWindow.UsedPercentage,
                    compatibilityWindow.Authority,
                    compatibilityWindow.Freshness)
                : PercentageUsageMetric.Unavailable(
                    DataAuthority.LocalProviderReported,
                    DataFreshness.Unknown),
            compatibilityWindow?.ResetAt,
            TokenCountMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            TokenCountMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            capturedAt,
            quotaWindows,
            activityMetrics: [],
            sourceObservedAt: capturedAt);
    }

    public static ProviderUsageSnapshot CreateUnavailable(DateTimeOffset capturedAtUtc) =>
        new(
            ProviderKind.Claude,
            ProviderConnectionState.Disconnected,
            PercentageUsageMetric.Unavailable(
                DataAuthority.LocalProviderReported,
                DataFreshness.Unknown),
            null,
            TokenCountMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            TokenCountMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            capturedAtUtc.ToUniversalTime(),
            quotaWindows: [],
            activityMetrics: []);

    private static void AddWindow(
        List<NormalizedQuotaWindow> windows,
        ClaudeBridgeQuotaWindowObservation? observation,
        string windowId,
        string displayLabel,
        TimeSpan duration,
        DateTimeOffset nowUtc,
        TimeSpan freshnessThreshold,
        TimeSpan futureTimestampTolerance)
    {
        if (observation is null)
        {
            return;
        }

        var observedAt = observation.ObservedAt.ToUniversalTime();
        if (observedAt - nowUtc > futureTimestampTolerance)
        {
            return;
        }

        var freshness = nowUtc - observedAt <= freshnessThreshold
            ? DataFreshness.Live
            : DataFreshness.Stale;

        windows.Add(
            new NormalizedQuotaWindow(
                ProviderKind.Claude,
                windowId,
                displayLabel,
                UsageMetricLabelOrigin.TokenFishDurationMapping,
                observation.UsedPercentage,
                observation.ResetAt,
                duration,
                UsageMetricAvailability.Available,
                observedAt,
                DataAuthority.LocalProviderReported,
                freshness,
                BridgeSource));
    }
}
