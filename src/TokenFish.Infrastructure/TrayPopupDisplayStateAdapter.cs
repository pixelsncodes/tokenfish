using System.Globalization;
using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public sealed class TrayPopupDisplayStateAdapter
{
    private static readonly ProviderKind[] ProviderOrder =
    [
        ProviderKind.Claude,
        ProviderKind.Codex
    ];

    private readonly TimeProvider _timeProvider;

    public TrayPopupDisplayStateAdapter(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TrayPopupDisplayState Create(
        AppSettings settings,
        ApplicationRuntimeStatus status,
        IProviderRuntimeSnapshotStore snapshotStore)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(snapshotStore);

        var snapshots = snapshotStore.GetCurrentSnapshots();
        var providers = GetEnabledProviders(settings.ProviderSelectionMode)
            .Select(provider => CreateProviderState(provider, snapshots))
            .ToArray();

        return new TrayPopupDisplayState(
            MapApplicationState(status),
            MapStatusText(status),
            providers);
    }

    private ProviderCardDisplayState CreateProviderState(
        ProviderKind provider,
        IReadOnlyDictionary<ProviderKind, ProviderRuntimeSnapshotState> snapshots)
    {
        if (!snapshots.TryGetValue(provider, out var state))
        {
            return new ProviderCardDisplayState(
                provider,
                GetProviderName(provider),
                "Waiting for first refresh",
                "Unavailable",
                null,
                "Unavailable",
                "Unavailable",
                "Unavailable",
                "Unavailable",
                "Unavailable");
        }

        var snapshot = state.Snapshot;
        return new ProviderCardDisplayState(
            provider,
            GetProviderName(provider),
            MapConnectionState(snapshot.ConnectionState),
            MapFreshness(state.EffectiveFreshness),
            snapshot.UsageWindow.PercentageConsumed,
            FormatPercentage(snapshot.UsageWindow),
            FormatReset(snapshot.UsageWindowResetAt),
            FormatTokenCount(snapshot.SessionTokens),
            FormatTokenCount(snapshot.WeeklyTokens),
            MapAuthority(snapshot));
    }

    private string FormatReset(DateTimeOffset? resetAt)
    {
        if (!resetAt.HasValue)
        {
            return "Unavailable";
        }

        var remaining = resetAt.Value.ToUniversalTime() - _timeProvider.GetUtcNow().ToUniversalTime();
        if (remaining <= TimeSpan.Zero)
        {
            return "Reset due";
        }

        if (remaining.TotalHours >= 1)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)remaining.TotalHours}h {remaining.Minutes}m");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, remaining.Minutes)}m");
    }

    private static string FormatPercentage(PercentageUsageMetric metric) =>
        metric.IsAvailable
            ? string.Create(CultureInfo.InvariantCulture, $"{metric.PercentageConsumed:0.#}%")
            : "Unavailable";

    private static string FormatTokenCount(TokenCountMetric metric) =>
        metric.IsAvailable
            ? metric.TokenCount!.Value.ToString("N0", CultureInfo.InvariantCulture)
            : "Unavailable";

    private static string MapAuthority(ProviderUsageSnapshot snapshot)
    {
        if (snapshot.UsageWindow.IsAvailable)
        {
            return MapAuthority(snapshot.UsageWindow.Authority);
        }

        if (snapshot.SessionTokens.IsAvailable)
        {
            return MapAuthority(snapshot.SessionTokens.Authority);
        }

        if (snapshot.WeeklyTokens.IsAvailable)
        {
            return MapAuthority(snapshot.WeeklyTokens.Authority);
        }

        return "Unavailable";
    }

    private static PopupApplicationDisplayState MapApplicationState(
        ApplicationRuntimeStatus status) =>
        status.State switch
        {
            ApplicationRuntimeState.Starting => PopupApplicationDisplayState.Starting,
            ApplicationRuntimeState.Running => PopupApplicationDisplayState.Running,
            ApplicationRuntimeState.Refreshing => PopupApplicationDisplayState.Refreshing,
            ApplicationRuntimeState.Stopping => PopupApplicationDisplayState.Stopping,
            ApplicationRuntimeState.Stopped => PopupApplicationDisplayState.Stopped,
            ApplicationRuntimeState.Faulted => status.Issue switch
            {
                ApplicationRuntimeIssue.StartupFailed => PopupApplicationDisplayState.StartupIssue,
                ApplicationRuntimeIssue.RefreshFailed => PopupApplicationDisplayState.RefreshIssue,
                ApplicationRuntimeIssue.ShutdownFailed => PopupApplicationDisplayState.ShutdownIssue,
                ApplicationRuntimeIssue.ShellFailed => PopupApplicationDisplayState.ShellIssue,
                _ => PopupApplicationDisplayState.RefreshIssue
            },
            _ => PopupApplicationDisplayState.Stopped
        };

    private static string MapStatusText(ApplicationRuntimeStatus status) =>
        status.State switch
        {
            ApplicationRuntimeState.Starting => "Starting TokenFish",
            ApplicationRuntimeState.Running => "Running",
            ApplicationRuntimeState.Refreshing => "Refreshing",
            ApplicationRuntimeState.Stopping => "Stopping",
            ApplicationRuntimeState.Stopped => "Stopped",
            ApplicationRuntimeState.Faulted => status.Issue switch
            {
                ApplicationRuntimeIssue.StartupFailed => "TokenFish could not start",
                ApplicationRuntimeIssue.RefreshFailed => "TokenFish could not refresh usage",
                ApplicationRuntimeIssue.ShutdownFailed => "TokenFish could not shut down cleanly",
                ApplicationRuntimeIssue.ShellFailed => "TokenFish shell integration needs attention",
                _ => "TokenFish needs attention"
            },
            _ => "Unavailable"
        };

    private static string GetProviderName(ProviderKind provider) =>
        provider switch
        {
            ProviderKind.Claude => "Claude",
            ProviderKind.Codex => "Codex",
            _ => "Provider"
        };

    private static string MapConnectionState(ProviderConnectionState state) =>
        state switch
        {
            ProviderConnectionState.Unknown => "Unknown",
            ProviderConnectionState.NotConfigured => "Not configured",
            ProviderConnectionState.Disconnected => "Disconnected",
            ProviderConnectionState.Connecting => "Connecting",
            ProviderConnectionState.Connected => "Connected",
            ProviderConnectionState.Degraded => "Degraded",
            _ => "Unknown"
        };

    private static string MapFreshness(DataFreshness freshness) =>
        freshness switch
        {
            DataFreshness.Unknown => "Unavailable",
            DataFreshness.Live => "Live",
            DataFreshness.Cached => "Cached",
            DataFreshness.Stale => "Stale",
            _ => "Unavailable"
        };

    private static string MapAuthority(DataAuthority authority) =>
        authority switch
        {
            DataAuthority.LocalProviderReported => "Provider-reported",
            DataAuthority.TokenFishDerived => "Locally calculated",
            DataAuthority.UserControlled => "Estimated",
            _ => "Unavailable"
        };

    private static ProviderKind[] GetEnabledProviders(ProviderSelectionMode providerSelectionMode) =>
        providerSelectionMode switch
        {
            ProviderSelectionMode.ClaudeOnly => [ProviderKind.Claude],
            ProviderSelectionMode.CodexOnly => [ProviderKind.Codex],
            ProviderSelectionMode.Both => ProviderOrder,
            _ => []
        };
}
