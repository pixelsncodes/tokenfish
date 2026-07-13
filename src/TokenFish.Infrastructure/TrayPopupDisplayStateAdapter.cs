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
    private readonly CultureInfo _culture;
    private readonly TimeZoneInfo _timeZone;

    public TrayPopupDisplayStateAdapter(
        TimeProvider? timeProvider = null,
        CultureInfo? culture = null,
        TimeZoneInfo? timeZone = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _culture = culture ?? CultureInfo.CurrentCulture;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public TrayPopupDisplayState Create(
        AppSettings settings,
        ApplicationRuntimeStatus status,
        IProviderRuntimeSnapshotStore snapshotStore)
    {
        return Create(
            settings,
            status,
            ProviderRefreshStatus.Initial,
            snapshotStore);
    }

    public TrayPopupDisplayState Create(
        AppSettings settings,
        ApplicationRuntimeStatus status,
        ProviderRefreshStatus refreshStatus,
        IProviderRuntimeSnapshotStore snapshotStore)
    {
        return Create(
            settings,
            status,
            refreshStatus,
            ManualRefreshCommandState.Available,
            snapshotStore);
    }

    public TrayPopupDisplayState Create(
        AppSettings settings,
        ApplicationRuntimeStatus status,
        ProviderRefreshStatus refreshStatus,
        ManualRefreshCommandState refreshCommandState,
        IProviderRuntimeSnapshotStore snapshotStore)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(refreshStatus);
        ArgumentNullException.ThrowIfNull(refreshCommandState);
        ArgumentNullException.ThrowIfNull(snapshotStore);

        var snapshots = snapshotStore.GetCurrentSnapshots();
        var providers = GetEnabledProviders(settings.ProviderSelectionMode)
            .Select(provider => CreateProviderState(provider, snapshots))
            .ToArray();

        return new TrayPopupDisplayState(
            MapApplicationState(status, refreshStatus),
            MapStatusText(status, refreshStatus),
            providers,
            refreshCommandState);
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
                emptyUsageMessage: null,
                footerText: string.Empty);
        }

        var snapshot = state.Snapshot;
        var quotaWindows = CreateQuotaWindows(snapshot);
        var activityRows = CreateActivityRows(snapshot);
        var hasAvailableMetrics = quotaWindows.Count > 0 || activityRows.Count > 0;
        return new ProviderCardDisplayState(
            provider,
            GetProviderName(provider),
            MapConnectionState(snapshot.ConnectionState),
            quotaWindows,
            activityRows,
            hasAvailableMetrics || snapshot.ConnectionState != ProviderConnectionState.Connected
                ? null
                : $"{GetProviderName(provider)} did not report usage data.",
            CreateFooterText(provider, snapshot, state.EffectiveFreshness),
            state.EffectiveFreshness == DataFreshness.Stale);
    }

    private IReadOnlyList<PopupQuotaWindowDisplayState> CreateQuotaWindows(
        ProviderUsageSnapshot snapshot)
    {
        var windows = snapshot.QuotaWindows
            .Where(window => window.IsAvailable)
            .ToArray();
        var displayWindows = new List<PopupQuotaWindowDisplayState>(windows.Length);

        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            var label = CreateQuotaLabel(window, index);
            var percentageText = $"{FormatDecimal(window.UsedPercentage!.Value)}% used";
            var relativeResetText = window.ResetAt.HasValue
                ? FormatRelativeReset(window.ResetAt.Value)
                : null;
            var exactResetText = window.ResetAt.HasValue
                ? FormatExactReset(window.ResetAt.Value)
                : null;

            displayWindows.Add(
                new PopupQuotaWindowDisplayState(
                    label,
                    percentageText,
                    window.UsedPercentage.Value,
                    $"{label}: {percentageText}",
                    relativeResetText,
                    exactResetText));
        }

        return displayWindows;
    }

    private static string CreateQuotaLabel(NormalizedQuotaWindow window, int availableIndex)
    {
        if (string.IsNullOrWhiteSpace(window.DisplayLabel))
        {
            return availableIndex == 0 ? "Usage" : "Additional usage";
        }

        if (window.LabelOrigin == UsageMetricLabelOrigin.ProviderSupplied &&
            ContainsUsageNoun(window.DisplayLabel))
        {
            return window.DisplayLabel;
        }

        return $"{window.DisplayLabel} usage";
    }

    private static bool ContainsUsageNoun(string label) =>
        label.Contains("usage", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("limit", StringComparison.OrdinalIgnoreCase);

    private string FormatRelativeReset(DateTimeOffset resetAt)
    {
        var remaining = resetAt.ToUniversalTime() - _timeProvider.GetUtcNow().ToUniversalTime();
        if (remaining <= TimeSpan.Zero)
        {
            return "Reset due";
        }

        if (remaining < TimeSpan.FromMinutes(1))
        {
            return "Resets in less than a minute";
        }

        if (remaining < TimeSpan.FromHours(1))
        {
            return $"Resets in {(int)remaining.TotalMinutes}m";
        }

        if (remaining < TimeSpan.FromHours(48))
        {
            var hours = (int)remaining.TotalHours;
            var minutes = remaining.Minutes;

            return minutes > 0
                ? $"Resets in {hours}h {minutes}m"
                : $"Resets in {hours}h";
        }

        var days = (int)remaining.TotalDays;
        var trailingHours = remaining.Hours;

        return trailingHours > 0
            ? $"Resets in {days}d {trailingHours}h"
            : $"Resets in {days}d";
    }

    private string FormatExactReset(DateTimeOffset resetAt)
    {
        var localReset = TimeZoneInfo.ConvertTime(resetAt, _timeZone);

        return localReset.ToString("dddd, MMMM d 'at' h:mm tt", _culture);
    }

    private IReadOnlyList<PopupActivityDisplayState> CreateActivityRows(
        ProviderUsageSnapshot snapshot) =>
        snapshot.ActivityMetrics
            .Where(metric => metric.IsAvailable)
            .Select(CreateActivityRow)
            .ToArray();

    private PopupActivityDisplayState CreateActivityRow(NormalizedActivityMetric metric)
    {
        var label = metric.Unit == UsageActivityUnit.Tokens ? "Tokens used" : "Activity";
        var intervalText = CreateActivityIntervalText(metric);
        var valueText = FormatCompactNumber(metric.Value!.Value);
        var automationName = CreateActivityAutomationName(label, metric, intervalText);

        return new PopupActivityDisplayState(
            label,
            intervalText,
            valueText,
            automationName);
    }

    private string? CreateActivityIntervalText(NormalizedActivityMetric metric)
    {
        if (!metric.IntervalStart.HasValue || !metric.IntervalEnd.HasValue)
        {
            return null;
        }

        var start = DateOnly.FromDateTime(metric.IntervalStart.Value.Date);
        var inclusiveEnd = DateOnly.FromDateTime(metric.IntervalEnd.Value.Date).AddDays(-1);

        return FormatDateRange(start, inclusiveEnd);
    }

    private string CreateActivityAutomationName(
        string label,
        NormalizedActivityMetric metric,
        string? intervalText)
    {
        _ = intervalText;
        var fullValue = metric.Value!.Value.ToString("N0", _culture);
        var interval = CreateActivityAccessibilityInterval(metric);
        var valueText = metric.Unit == UsageActivityUnit.Tokens
            ? $"{fullValue} tokens used"
            : $"{fullValue} {label.ToLowerInvariant()}";

        return interval is null
            ? valueText
            : $"{valueText} from {interval}";
    }

    private string? CreateActivityAccessibilityInterval(NormalizedActivityMetric metric)
    {
        if (!metric.IntervalStart.HasValue || !metric.IntervalEnd.HasValue)
        {
            return null;
        }

        var start = DateOnly.FromDateTime(metric.IntervalStart.Value.Date);
        var inclusiveEnd = DateOnly.FromDateTime(metric.IntervalEnd.Value.Date).AddDays(-1);

        var includeYear = start.Year != inclusiveEnd.Year;

        if (start == inclusiveEnd)
        {
            return FormatLongDate(start, includeYear);
        }

        return $"{FormatLongDate(start, includeYear)} through {FormatLongDate(inclusiveEnd, includeYear)}";
    }

    private string FormatDateRange(DateOnly start, DateOnly end)
    {
        if (start == end)
        {
            return FormatShortDate(start, includeYear: false);
        }

        if (start.Year != end.Year)
        {
            return $"{FormatShortDate(start, includeYear: true)}–{FormatShortDate(end, includeYear: true)}";
        }

        if (start.Month == end.Month)
        {
            return $"{start.ToDateTime(TimeOnly.MinValue).ToString("MMM", _culture)} {start.Day}–{end.Day}";
        }

        return $"{FormatShortDate(start, includeYear: false)}–{FormatShortDate(end, includeYear: false)}";
    }

    private string FormatShortDate(DateOnly date, bool includeYear) =>
        date.ToDateTime(TimeOnly.MinValue).ToString(includeYear ? "MMM d, yyyy" : "MMM d", _culture);

    private string FormatLongDate(DateOnly date, bool includeYear = false) =>
        date.ToDateTime(TimeOnly.MinValue).ToString(includeYear ? "MMMM d, yyyy" : "MMMM d", _culture);

    private static string FormatCompactNumber(long value)
    {
        if (value < 1_000)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return value switch
        {
            < 1_000_000 => $"{FormatDecimal(value / 1_000m)}K",
            < 1_000_000_000 => $"{FormatDecimal(value / 1_000_000m)}M",
            _ => $"{FormatDecimal(value / 1_000_000_000m)}B"
        };
    }

    private string CreateFooterText(
        ProviderKind provider,
        ProviderUsageSnapshot snapshot,
        DataFreshness effectiveFreshness)
    {
        var capturedAtValues = snapshot.QuotaWindows
            .Where(window => window.IsAvailable)
            .Select(window => window.CapturedAt)
            .Concat(snapshot.ActivityMetrics
                .Where(metric => metric.IsAvailable)
                .Select(metric => metric.CapturedAt))
            .ToArray();
        if (capturedAtValues.Length == 0)
        {
            return string.Empty;
        }

        var capturedAt = capturedAtValues.Max();

        var text = $"{FormatUpdatedText(capturedAt)} · Reported by {GetProviderName(provider)}";
        return effectiveFreshness == DataFreshness.Stale
            ? $"{text} · May be outdated"
            : text;
    }

    private string FormatUpdatedText(DateTimeOffset capturedAt)
    {
        var age = _timeProvider.GetUtcNow().ToUniversalTime() - capturedAt.ToUniversalTime();
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return "Updated just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"Updated {(int)age.TotalMinutes} min ago";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"Updated {(int)age.TotalHours}h ago";
        }

        var localCapturedAt = TimeZoneInfo.ConvertTime(capturedAt, _timeZone);
        return $"Updated {localCapturedAt.ToString("g", _culture)}";
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);

    private static PopupApplicationDisplayState MapApplicationState(
        ApplicationRuntimeStatus status,
        ProviderRefreshStatus refreshStatus)
    {
        if (status.State is ApplicationRuntimeState.Stopping or ApplicationRuntimeState.Stopped)
        {
            return status.State == ApplicationRuntimeState.Stopping
                ? PopupApplicationDisplayState.Stopping
                : PopupApplicationDisplayState.Stopped;
        }

        if (status.State == ApplicationRuntimeState.Faulted &&
            status.Issue != ApplicationRuntimeIssue.RefreshFailed)
        {
            return status.Issue switch
            {
                ApplicationRuntimeIssue.StartupFailed => PopupApplicationDisplayState.StartupIssue,
                ApplicationRuntimeIssue.ShutdownFailed => PopupApplicationDisplayState.ShutdownIssue,
                ApplicationRuntimeIssue.ShellFailed => PopupApplicationDisplayState.ShellIssue,
                _ => PopupApplicationDisplayState.RefreshIssue
            };
        }

        if (refreshStatus.IsRefreshActive)
        {
            return PopupApplicationDisplayState.Refreshing;
        }

        if (refreshStatus.Outcome == ProviderRefreshOutcome.Failed ||
            status.Issue == ApplicationRuntimeIssue.RefreshFailed)
        {
            return PopupApplicationDisplayState.RefreshIssue;
        }

        return status.State switch
        {
            ApplicationRuntimeState.Starting => PopupApplicationDisplayState.Starting,
            ApplicationRuntimeState.Running => PopupApplicationDisplayState.Running,
            ApplicationRuntimeState.Refreshing => PopupApplicationDisplayState.Refreshing,
            _ => PopupApplicationDisplayState.Stopped
        };
    }

    private string MapStatusText(
        ApplicationRuntimeStatus status,
        ProviderRefreshStatus refreshStatus)
    {
        if (status.State is ApplicationRuntimeState.Stopping or ApplicationRuntimeState.Stopped)
        {
            return status.State == ApplicationRuntimeState.Stopping ? "Stopping" : "Stopped";
        }

        if (status.State == ApplicationRuntimeState.Faulted &&
            status.Issue != ApplicationRuntimeIssue.RefreshFailed)
        {
            return status.Issue switch
            {
                ApplicationRuntimeIssue.StartupFailed => "TokenFish could not start",
                ApplicationRuntimeIssue.ShutdownFailed => "TokenFish could not shut down cleanly",
                ApplicationRuntimeIssue.ShellFailed => "TokenFish shell integration needs attention",
                _ => "TokenFish needs attention"
            };
        }

        if (refreshStatus.IsRefreshActive)
        {
            return "Updating…";
        }

        if (refreshStatus.Outcome == ProviderRefreshOutcome.Failed ||
            status.Issue == ApplicationRuntimeIssue.RefreshFailed)
        {
            return "Update failed";
        }

        if (refreshStatus.LatestSucceededAtUtc.HasValue)
        {
            return FormatUpdatedText(refreshStatus.LatestSucceededAtUtc.Value);
        }

        if (refreshStatus.Outcome == ProviderRefreshOutcome.NeverRefreshed &&
            status.State == ApplicationRuntimeState.Running)
        {
            return "Waiting for first update";
        }

        return status.State switch
        {
            ApplicationRuntimeState.Starting => "Starting TokenFish",
            ApplicationRuntimeState.Running => "Running",
            ApplicationRuntimeState.Refreshing => "Refreshing",
            ApplicationRuntimeState.Stopped => "Stopped",
            _ => "Unavailable"
        };
    }

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

    private static ProviderKind[] GetEnabledProviders(ProviderSelectionMode providerSelectionMode) =>
        providerSelectionMode switch
        {
            ProviderSelectionMode.ClaudeOnly => [ProviderKind.Claude],
            ProviderSelectionMode.CodexOnly => [ProviderKind.Codex],
            ProviderSelectionMode.Both => ProviderOrder,
            _ => []
        };
}
