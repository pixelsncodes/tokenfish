using System.Globalization;
using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class TrayPopupDisplayStateAdapterTests
{
    private static readonly CultureInfo TestCulture = CultureInfo.GetCultureInfo("en-US");
    [Theory]
    [InlineData(UsageActivityUnit.Days, "Current streak", "Current streak: 28 days")]
    [InlineData(UsageActivityUnit.Seconds, "Longest turn", "Longest turn: 28 seconds")]
    public void SummaryAccessibilityNamesIncludeReportedUnits(UsageActivityUnit unit, string label, string expected)
    {
        var metric = new NormalizedActivityMetric(ProviderKind.Codex, "codex:summary:test", label,
            UsageMetricLabelOrigin.ProviderSupplied, 28, unit, null, null,
            UsageMetricAvailability.Available, DateTimeOffset.UtcNow, DataAuthority.LocalProviderReported,
            DataFreshness.Live, "account/usage/read");
        var provider = SingleProvider(CreateSnapshot(ProviderKind.Codex, activityMetrics: [metric]));
        Assert.Equal(expected, Assert.Single(provider.ActivityRows).AutomationName);
    }
    private static readonly TimeZoneInfo TestTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "TokenFishTestTime",
        TimeSpan.FromHours(-7),
        "TokenFish Test Time",
        "TokenFish Test Time");

    [Fact]
    public void DisabledProvidersAreOmitted()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            []);

        Assert.Equal([ProviderKind.Codex], state.Providers.Select(provider => provider.Provider));
    }

    [Fact]
    public void CodexOnlySettingsShowOnlyCodex()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [CreateSnapshot(ProviderKind.Codex), CreateSnapshot(ProviderKind.Claude)]);

        Assert.Equal([ProviderKind.Codex], state.Providers.Select(provider => provider.Provider));
    }

    [Fact]
    public void ClaudeOnlySettingsDoNotShowCodex()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly },
            [CreateSnapshot(ProviderKind.Codex), CreateSnapshot(ProviderKind.Claude)]);

        Assert.Equal([ProviderKind.Claude], state.Providers.Select(provider => provider.Provider));
    }

    [Fact]
    public void BothProviderSettingsPreserveConsistentOrder()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.Both },
            [CreateSnapshot(ProviderKind.Codex), CreateSnapshot(ProviderKind.Claude)]);

        Assert.Equal([ProviderKind.Codex, ProviderKind.Claude], state.Providers.Select(provider => provider.Provider));
    }

    [Fact]
    public void ClaudePopupDisplaysBothReportedQuotaWindows()
    {
        var snapshot = CreateSnapshot(
            ProviderKind.Claude,
            sessionTokens: null,
            weeklyTokens: null,
            quotaWindows:
            [
                ClaudeQuotaWindow("claude:status-line:five-hour", "5h", 24m),
                ClaudeQuotaWindow("claude:status-line:seven-day", "7d", 41m)
            ],
            activityMetrics: []);
        var provider = SingleProvider(
            snapshot,
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly });

        Assert.Equal(2, snapshot.QuotaWindows.Count);
        Assert.Contains(snapshot.QuotaWindows, window => window.WindowId == "claude:status-line:seven-day");
        Assert.Equal("Claude", provider.ProviderName);
        Assert.Equal(["5h usage","7d usage"], provider.QuotaWindows.Select(window => window.Label));
        Assert.Equal(["24% used","41% used"], provider.QuotaWindows.Select(window => window.PercentageText));
        Assert.Equal(["76% left","59% left"],provider.QuotaWindows.Select(window=>window.RemainingText));
        Assert.Empty(provider.ActivityRows);
    }

    [Fact]
    public void DisabledClaudeSnapshotRemainsAbsentFromPopup()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [
                CreateSnapshot(ProviderKind.Claude),
                CreateSnapshot(ProviderKind.Codex)
            ]);

        Assert.DoesNotContain(state.Providers, provider => provider.Provider == ProviderKind.Claude);
    }

    [Fact]
    public void NoSnapshotMapsToWaitingConnectionWithoutUsageRows()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            []);
        var provider = Assert.Single(state.Providers);

        Assert.Equal("Waiting for first refresh", provider.ConnectionState);
        Assert.Empty(provider.QuotaWindows);
        Assert.Empty(provider.ActivityRows);
        Assert.Null(provider.EmptyUsageMessage);
        Assert.Equal(string.Empty, provider.FooterText);
    }

    [Fact]
    public void InitialPopupProjectionBeforeFirstRefreshDoesNotThrow()
    {
        var exception = Record.Exception(() => CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Starting));

        Assert.Null(exception);
    }

    [Fact]
    public void WaitingForFirstRefreshStateUsesNormalStartingPresentation()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Starting);
        var provider = Assert.Single(state.Providers);

        Assert.Equal(PopupApplicationDisplayState.Starting, state.ApplicationState);
        Assert.Equal("Starting TokenFish", state.StatusText);
        Assert.Equal("Waiting for first refresh", provider.ConnectionState);
        Assert.Empty(provider.QuotaWindows);
        Assert.Empty(provider.ActivityRows);
        Assert.Null(provider.EmptyUsageMessage);
        Assert.Equal(string.Empty, provider.FooterText);
    }

    [Fact]
    public void EmptyNormalizedQuotaAndActivityCollectionsMapToNoDataState()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows: [],
            activityMetrics: []));

        Assert.Equal("Connected", provider.ConnectionState);
        Assert.Empty(provider.QuotaWindows);
        Assert.Empty(provider.ActivityRows);
        Assert.Equal("Codex did not report usage data.", provider.EmptyUsageMessage);
        Assert.Equal(string.Empty, provider.FooterText);
    }

    [Fact]
    public void UnavailableNormalizedMetricsWithoutAvailableCaptureDataOmitFooter()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            connectionState: ProviderConnectionState.Disconnected,
            quotaWindows:
            [
                NormalizedQuotaWindow.Unavailable(
                    ProviderKind.Codex,
                    "codex:default:primary",
                    capturedAt,
                    DataAuthority.LocalProviderReported,
                    DataFreshness.Unknown,
                    "account/rateLimits/read")
            ],
            activityMetrics:
            [
                NormalizedActivityMetric.Unavailable(
                    ProviderKind.Codex,
                    "codex:activity:latest-seven-utc-dates:tokens",
                    UsageActivityUnit.Tokens,
                    capturedAt,
                    DataAuthority.TokenFishDerived,
                    DataFreshness.Unknown,
                    "account/usage/read")
            ]));

        Assert.Equal("Disconnected", provider.ConnectionState);
        Assert.Empty(provider.QuotaWindows);
        Assert.Empty(provider.ActivityRows);
        Assert.Null(provider.EmptyUsageMessage);
        Assert.Equal(string.Empty, provider.FooterText);
    }

    [Theory]
    [InlineData(ApplicationRuntimeState.Starting)]
    [InlineData(ApplicationRuntimeState.Running)]
    [InlineData(ApplicationRuntimeState.Faulted)]
    public void MainPopupStateConstructionDoesNotThrowForEmptyRuntimeData(
        ApplicationRuntimeState runtimeState)
    {
        var status = runtimeState == ApplicationRuntimeState.Faulted
            ? ApplicationRuntimeStatus.RefreshFaulted
            : new ApplicationRuntimeStatus(runtimeState, ApplicationRuntimeIssue.None);

        var exception = Record.Exception(() => CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            status));

        Assert.Null(exception);
    }

    [Fact]
    public void PresentationModelDoesNotExposeLegacyTechnicalRows()
    {
        var propertyNames = typeof(ProviderCardDisplayState)
            .GetProperties()
            .Select(property => property.Name);

        Assert.DoesNotContain("UsageWindow", propertyNames);
        Assert.DoesNotContain("Reset", propertyNames);
        Assert.DoesNotContain("SessionTokens", propertyNames);
        Assert.DoesNotContain("WeeklyTokens", propertyNames);
        Assert.DoesNotContain("Freshness", propertyNames);
        Assert.DoesNotContain("DataAuthority", propertyNames);
    }

    [Fact]
    public void ApplicationFaultsExposeOnlyFixedSafeUiText()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.RefreshFaulted);

        Assert.Equal(PopupApplicationDisplayState.RefreshIssue, state.ApplicationState);
        Assert.Equal("Update failed", state.StatusText);
        Assert.DoesNotContain("exception", state.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NeverRefreshedRunningStateShowsWaitingStatus()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Running,
            refreshStatus: ProviderRefreshStatus.Initial);

        Assert.Equal(PopupApplicationDisplayState.Running, state.ApplicationState);
        Assert.Equal("Waiting for first update", state.StatusText);
    }

    [Fact]
    public void IdlePopupStateShowsEnabledRefreshNow()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Running,
            refreshCommandState: ManualRefreshCommandState.Available);

        Assert.True(state.RefreshCommandState.IsEnabled);
        Assert.Equal("Refresh now", state.RefreshCommandState.Label);
        Assert.Equal(string.Empty, state.RefreshCommandState.StatusText);
    }

    [Fact]
    public void ActivePopupStateShowsDisabledRefreshing()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Running,
            refreshStatus: RefreshingStatus(),
            refreshCommandState: ManualRefreshCommandState.Refreshing);

        Assert.False(state.RefreshCommandState.IsEnabled);
        Assert.Equal("Refreshing…", state.RefreshCommandState.Label);
        Assert.Equal("Refreshing usage", state.RefreshCommandState.StatusText);
    }

    [Fact]
    public void ActiveRefreshWithNoPriorValuesShowsUpdatingAndWaitingProvider()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Running,
            refreshStatus: RefreshingStatus());
        var provider = Assert.Single(state.Providers);

        Assert.Equal(PopupApplicationDisplayState.Refreshing, state.ApplicationState);
        Assert.Equal("Updating…", state.StatusText);
        Assert.Equal("Waiting for first refresh", provider.ConnectionState);
        Assert.Empty(provider.QuotaWindows);
        Assert.Empty(provider.ActivityRows);
        Assert.False(state.RefreshCommandState.IsEnabled);
    }

    [Fact]
    public void ActiveRefreshKeepsPriorValidValuesVisible()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [CreateSnapshot(
                ProviderKind.Codex,
                quotaWindows: [QuotaWindow("codex:default:primary", "Weekly", 14m)],
                activityMetrics: [ActivityMetric(100)])],
            ApplicationRuntimeStatus.Running,
            refreshStatus: RefreshingStatus());
        var provider = Assert.Single(state.Providers);

        Assert.Equal("Updating…", state.StatusText);
        var quotaWindow = Assert.Single(provider.QuotaWindows);
        Assert.Equal("14% used", quotaWindow.PercentageText);
        Assert.Equal(14m, quotaWindow.ProgressValue);
        Assert.Equal("100", Assert.Single(provider.ActivityRows).ValueText);
    }

    [Fact]
    public void SuccessfulRefreshShowsLastUpdatedFromRefreshStatus()
    {
        var now = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Running,
            new ManualTimeProvider(now),
            SucceededStatus(now));

        Assert.Equal(PopupApplicationDisplayState.Running, state.ApplicationState);
        Assert.Equal("Updated just now", state.StatusText);
    }

    [Fact]
    public void SuccessfulRefreshUsesLocalDisplayTimeForOlderSuccess()
    {
        var now = new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);
        var succeededAt = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Running,
            new ManualTimeProvider(now),
            SucceededStatus(succeededAt));

        Assert.Equal("Updated 7/12/2026 1:00 AM", state.StatusText);
    }

    [Fact]
    public void FailedRefreshRetainsPriorValidValuesAndUsesSafeStatus()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [CreateSnapshot(
                ProviderKind.Codex,
                quotaWindows: [QuotaWindow("codex:default:primary", "Weekly", 14m)])],
            ApplicationRuntimeStatus.Running,
            refreshStatus: FailedStatus());
        var provider = Assert.Single(state.Providers);

        Assert.Equal(PopupApplicationDisplayState.RefreshIssue, state.ApplicationState);
        Assert.Equal("Update failed", state.StatusText);
        Assert.Equal("14% used", Assert.Single(provider.QuotaWindows).PercentageText);
        Assert.DoesNotContain("C:\\Users", state.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", state.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Refresh now", state.RefreshCommandState.Label);
        Assert.True(state.RefreshCommandState.IsEnabled);
    }

    [Fact]
    public void FailedRefreshExposesNoSensitiveDecoyException()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [CreateSnapshot(
                ProviderKind.Codex,
                quotaWindows: [QuotaWindow("codex:default:primary", "Weekly", 14m)])],
            ApplicationRuntimeStatus.Running,
            refreshStatus: FailedStatus());

        var displayText = string.Join(
            " ",
            state.StatusText,
            state.RefreshCommandState.Label,
            state.RefreshCommandState.StatusText,
            string.Join(" ", state.Providers.Select(provider => provider.FooterText)));

        Assert.DoesNotContain("C:\\Users\\pixel", displayText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/mnt/c/Users/pixel", displayText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", displayText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", displayText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SuccessClearsPreviousFailureStateAndRestoresNormalButtonState()
    {
        var now = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.Running,
            new ManualTimeProvider(now),
            SucceededStatus(now),
            ManualRefreshCommandState.Available);

        Assert.Equal(PopupApplicationDisplayState.Running, state.ApplicationState);
        Assert.Equal("Updated just now", state.StatusText);
        Assert.True(state.RefreshCommandState.IsEnabled);
        Assert.Equal("Refresh now", state.RefreshCommandState.Label);
    }

    [Fact]
    public void FailedFirstRefreshWithUnavailableMetricsDoesNotFabricateValues()
    {
        var capturedAt = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [CreateSnapshot(
                ProviderKind.Codex,
                connectionState: ProviderConnectionState.Disconnected,
                quotaWindows:
                [
                    NormalizedQuotaWindow.Unavailable(
                        ProviderKind.Codex,
                        "codex:default:primary",
                        capturedAt,
                        DataAuthority.LocalProviderReported,
                        DataFreshness.Unknown,
                        "account/rateLimits/read")
                ],
                activityMetrics:
                [
                    NormalizedActivityMetric.Unavailable(
                        ProviderKind.Codex,
                        "codex:activity:latest-seven-utc-dates:tokens",
                        UsageActivityUnit.Tokens,
                        capturedAt,
                        DataAuthority.TokenFishDerived,
                        DataFreshness.Unknown,
                        "account/usage/read")
                ])],
            ApplicationRuntimeStatus.Running,
            refreshStatus: FailedStatus());
        var provider = Assert.Single(state.Providers);

        Assert.Equal("Update failed", state.StatusText);
        Assert.Equal("Disconnected", provider.ConnectionState);
        Assert.Empty(provider.QuotaWindows);
        Assert.Empty(provider.ActivityRows);
        Assert.Null(provider.EmptyUsageMessage);
    }

    [Fact]
    public void DisconnectedProviderWithoutMetricsDoesNotShowUsageEmptyState()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            connectionState: ProviderConnectionState.Disconnected));

        Assert.Equal("Disconnected", provider.ConnectionState);
        Assert.Null(provider.EmptyUsageMessage);
    }

    [Fact]
    public void OneLabeledQuotaWindowProjectsDisplayState()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows: [QuotaWindow("codex:default:primary", "Weekly", 14m)]));

        var quota = Assert.Single(provider.QuotaWindows);
        Assert.Equal("Weekly usage", quota.Label);
        Assert.Equal("14% used", quota.PercentageText);
        Assert.Equal(14m, quota.ProgressValue);
        Assert.Equal("Weekly usage: 14% used", quota.ProgressAutomationName);
    }

    [Fact]
    public void OneUnlabeledQuotaWindowUsesNeutralUsageLabel()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows: [QuotaWindow("codex:default:primary", displayLabel: null, usedPercentage: 14m)]));

        Assert.Equal("Usage", Assert.Single(provider.QuotaWindows).Label);
    }

    [Fact]
    public void MultipleQuotaWindowsUseStableNormalizedOrder()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows:
            [
                QuotaWindow("codex:default:secondary", displayLabel: null, usedPercentage: 30m),
                QuotaWindow("codex:default:primary", "5h", 20m)
            ]));

        Assert.Equal(
            ["Usage", "5h usage"],
            provider.QuotaWindows.Select(window => window.Label));
        Assert.Equal(
            ["30% used", "20% used"],
            provider.QuotaWindows.Select(window => window.PercentageText));
    }

    [Fact]
    public void SubsequentUnlabeledQuotaWindowsUseAdditionalUsageLabel()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows:
            [
                QuotaWindow("codex:default:primary", displayLabel: null, usedPercentage: 10m),
                QuotaWindow("codex:default:secondary", displayLabel: null, usedPercentage: 20m)
            ]));

        Assert.Equal(
            ["Usage", "Additional usage"],
            provider.QuotaWindows.Select(window => window.Label));
    }

    [Fact]
    public void PercentageFormattingUsesAtMostOneDecimalAndProgressIsNotInverted()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows: [QuotaWindow("codex:default:primary", "Weekly", 14.56m)]));

        var quota = Assert.Single(provider.QuotaWindows);
        Assert.Equal("14.6% used", quota.PercentageText);
        Assert.Equal(14.56m, quota.ProgressValue);
    }

    [Theory]
    [InlineData(30, "Resets in less than a minute")]
    [InlineData(42 * 60, "Resets in 42m")]
    [InlineData((5 * 60 * 60) + (20 * 60), "Resets in 5h 20m")]
    [InlineData(5 * 60 * 60, "Resets in 5h")]
    [InlineData((6 * 24 * 60 * 60) + (21 * 60 * 60), "Resets in 6d 21h")]
    [InlineData(2 * 24 * 60 * 60, "Resets in 2d")]
    [InlineData(0, "Reset due")]
    public void RelativeResetTextUsesBoundedUnits(int secondsUntilReset, string expected)
    {
        var now = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var provider = SingleProvider(
            CreateSnapshot(
                ProviderKind.Codex,
                quotaWindows:
                [
                    QuotaWindow(
                        "codex:default:primary",
                        "Weekly",
                        14m,
                        resetAt: now.AddSeconds(secondsUntilReset))
                ]),
            new ManualTimeProvider(now));

        Assert.Equal(expected, Assert.Single(provider.QuotaWindows).RelativeResetText);
    }

    [Fact]
    public void MissingResetOmitsResetPresentation()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows:
            [
                QuotaWindow(
                    "codex:default:primary",
                    "Weekly",
                    14m,
                    hasReset: false)
            ]));

        var quota = Assert.Single(provider.QuotaWindows);
        Assert.Null(quota.RelativeResetText);
        Assert.Null(quota.ExactResetText);
    }

    [Fact]
    public void UnavailableNormalizedQuotaWindowsAreOmitted()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows:
            [
                NormalizedQuotaWindow.Unavailable(
                    ProviderKind.Codex,
                    "codex:default:primary",
                    new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
                    DataAuthority.LocalProviderReported,
                    DataFreshness.Unknown,
                    "account/rateLimits/read")
            ]));

        Assert.Empty(provider.QuotaWindows);
    }

    [Fact]
    public void ExactResetTextUsesInjectedCultureAndTimeZone()
    {
        var resetAt = new DateTimeOffset(2026, 7, 19, 22, 0, 0, TimeSpan.Zero);
        var provider = SingleProvider(
            CreateSnapshot(
                ProviderKind.Codex,
                quotaWindows:
                [
                    QuotaWindow(
                        "codex:default:primary",
                        "Weekly",
                        14m,
                        resetAt)
                ]),
            timeProvider: new ManualTimeProvider(new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero)));

        Assert.Equal("Sunday, July 19 at 3:00 PM", Assert.Single(provider.QuotaWindows).ExactResetText);
    }

    [Theory]
    [InlineData(999, "999")]
    [InlineData(1_234, "1.2K")]
    [InlineData(105_198_188, "105.2M")]
    [InlineData(1_100_000_000, "1.1B")]
    public void ActivityValuesUseCompactFormatting(long value, string expected)
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            activityMetrics: [ActivityMetric(value)]));

        Assert.Equal(expected, Assert.Single(provider.ActivityRows).ValueText);
    }

    [Fact]
    public void SameDayActivityIntervalFormatsCompactly()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            activityMetrics: [ActivityMetric(100, new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 12))]));

        Assert.Equal("Jul 12", Assert.Single(provider.ActivityRows).IntervalText);
    }

    [Fact]
    public void SameMonthActivityIntervalFormatsCompactly()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            activityMetrics: [ActivityMetric(100, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 12))]));

        Assert.Equal("Jul 6–12", Assert.Single(provider.ActivityRows).IntervalText);
    }

    [Fact]
    public void CrossMonthActivityIntervalFormatsCompactly()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            activityMetrics: [ActivityMetric(100, new DateOnly(2026, 6, 29), new DateOnly(2026, 7, 5))]));

        Assert.Equal("Jun 29–Jul 5", Assert.Single(provider.ActivityRows).IntervalText);
    }

    [Fact]
    public void CrossYearActivityIntervalIncludesYears()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            activityMetrics: [ActivityMetric(100, new DateOnly(2025, 12, 29), new DateOnly(2026, 1, 4))]));

        Assert.Equal("Dec 29, 2025–Jan 4, 2026", Assert.Single(provider.ActivityRows).IntervalText);
    }

    [Fact]
    public void ActivityAutomationTextUsesFullValueAndInterval()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            activityMetrics: [ActivityMetric(105_198_188, new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 12))]));

        Assert.Equal(
            "105,198,188 tokens used from July 6 through July 12",
            Assert.Single(provider.ActivityRows).AutomationName);
    }

    [Fact]
    public void ActivityPresentationDoesNotImplyWeeklyQuota()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            activityMetrics: [ActivityMetric(105_198_188)]));

        var activity = Assert.Single(provider.ActivityRows);
        Assert.Equal("Tokens used", activity.Label);
        Assert.Equal("Jul 6–12", activity.IntervalText);
        Assert.DoesNotContain("Weekly", activity.Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quota", activity.AutomationName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingActivityOmitsActivityRows()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows: [QuotaWindow("codex:default:primary", "Weekly", 14m)]));

        Assert.Empty(provider.ActivityRows);
    }

    [Fact]
    public void ActivityWithoutQuotaDoesNotCreateEmptyState()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            activityMetrics: [ActivityMetric(100)]));

        Assert.Empty(provider.QuotaWindows);
        Assert.Single(provider.ActivityRows);
        Assert.Null(provider.EmptyUsageMessage);
    }

    [Fact]
    public void ConnectedProviderWithNoAvailableMetricsGetsNeutralEmptyState()
    {
        var provider = SingleProvider(CreateSnapshot(ProviderKind.Codex));

        Assert.Empty(provider.QuotaWindows);
        Assert.Empty(provider.ActivityRows);
        Assert.Equal("Codex did not report usage data.", provider.EmptyUsageMessage);
        Assert.Equal(string.Empty, provider.FooterText);
    }

    [Theory]
    [InlineData(0, "Updated just now · Reported by Codex")]
    [InlineData(8 * 60, "Updated 8 min ago · Reported by Codex")]
    [InlineData(2 * 60 * 60, "Updated 2h ago · Reported by Codex")]
    public void FooterTextSummarizesFreshUpdateAge(int secondsSinceCapture, string expected)
    {
        var now = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var provider = SingleProvider(
            CreateSnapshot(
                ProviderKind.Codex,
                capturedAt: now.AddSeconds(-secondsSinceCapture),
                quotaWindows:
                [
                    QuotaWindow(
                        "codex:default:primary",
                        "Weekly",
                        14m,
                        capturedAt: now.AddSeconds(-secondsSinceCapture))
                ]),
            new ManualTimeProvider(now));

        Assert.Equal(expected, provider.FooterText);
        Assert.False(provider.IsStale);
    }

    [Fact]
    public void StaleFooterTextAddsOutdatedHint()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var store = new InMemoryProviderRuntimeSnapshotStore(timeProvider, TimeSpan.FromMinutes(5));
        store.Store(
        [
            CreateSnapshot(
                ProviderKind.Codex,
                capturedAt: timeProvider.GetUtcNow(),
                quotaWindows:
                [
                    QuotaWindow(
                        "codex:default:primary",
                        "Weekly",
                        14m,
                        capturedAt: timeProvider.GetUtcNow())
                ])
        ]);
        timeProvider.Advance(TimeSpan.FromMinutes(6));

        var state = new TrayPopupDisplayStateAdapter(
            timeProvider,
            TestCulture,
            TestTimeZone).Create(
                new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
                ApplicationRuntimeStatus.Running,
                store);

        var provider = Assert.Single(state.Providers);
        Assert.Equal("Updated 6 min ago · Reported by Codex · May be outdated", provider.FooterText);
        Assert.True(provider.IsStale);
    }

    [Fact]
    public void StaleClaudeSourceObservationAddsAccurateOutdatedHint()
    {
        var now = new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);
        var observedAt = now.AddMinutes(-6);
        var provider = SingleProvider(
            CreateSnapshot(
                ProviderKind.Claude,
                capturedAt: observedAt,
                quotaWindows:
                [
                    ClaudeQuotaWindow(
                        "claude:status-line:five-hour",
                        "5h",
                        14m,
                        observedAt)
                ],
                sourceObservedAt: observedAt),
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly },
            new ManualTimeProvider(now));

        Assert.Equal("Updated 6 min ago · Reported by Claude · May be outdated", provider.FooterText);
        Assert.True(provider.IsStale);
    }

    [Fact]
    public void FooterDoesNotExposeInternalProvenanceEnums()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            quotaWindows: [QuotaWindow("codex:default:primary", "Weekly", 14m)]));

        Assert.DoesNotContain(nameof(DataAuthority.LocalProviderReported), provider.FooterText);
        Assert.DoesNotContain(nameof(DataFreshness.Live), provider.FooterText);
        Assert.DoesNotContain("account/rateLimits/read", provider.FooterText);
    }

    [Fact]
    public void ProjectionDoesNotCreateSessionActivity()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            sessionTokens: 123,
            activityMetrics: []));

        Assert.Empty(provider.ActivityRows);
    }

    [Fact]
    public void NormalizedProjectionDoesNotDependOnLegacyCompatibilityFields()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            percentage: 99m,
            weeklyTokens: 999_999_999,
            quotaWindows: [QuotaWindow("codex:default:primary", "Weekly", 14m)],
            activityMetrics: [ActivityMetric(123)]));

        Assert.Equal("14% used", Assert.Single(provider.QuotaWindows).PercentageText);
        Assert.Equal("123", Assert.Single(provider.ActivityRows).ValueText);
    }

    private static ProviderCardDisplayState SingleProvider(
        ProviderUsageSnapshot snapshot,
        TimeProvider? timeProvider = null)
    {
        return SingleProvider(
            snapshot,
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            timeProvider);
    }

    private static ProviderCardDisplayState SingleProvider(
        ProviderUsageSnapshot snapshot,
        AppSettings settings,
        TimeProvider? timeProvider = null)
    {
        var state = CreateState(
            settings,
            [snapshot],
            ApplicationRuntimeStatus.Running,
            timeProvider);

        return Assert.Single(state.Providers);
    }

    private static TrayPopupDisplayState CreateState(
        AppSettings settings,
        IReadOnlyList<ProviderUsageSnapshot> snapshots,
        ApplicationRuntimeStatus? status = null,
        TimeProvider? timeProvider = null,
        ProviderRefreshStatus? refreshStatus = null,
        ManualRefreshCommandState? refreshCommandState = null)
    {
        timeProvider ??= new ManualTimeProvider();
        var store = new InMemoryProviderRuntimeSnapshotStore(timeProvider, TimeSpan.FromMinutes(5));
        if (snapshots.Count > 0)
        {
            store.Store(snapshots);
        }

        return new TrayPopupDisplayStateAdapter(timeProvider, TestCulture, TestTimeZone).Create(
            settings,
            status ?? ApplicationRuntimeStatus.Running,
            refreshStatus ?? ProviderRefreshStatus.Initial,
            refreshCommandState ?? (
                refreshStatus?.IsRefreshActive == true
                    ? ManualRefreshCommandState.Refreshing
                    : ManualRefreshCommandState.Available),
            store);
    }

    private static ProviderRefreshStatus RefreshingStatus() =>
        new(
            IsRefreshActive: true,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: null,
            ProviderRefreshOutcome.Refreshing,
            Version: 1);

    private static ProviderRefreshStatus SucceededStatus(DateTimeOffset succeededAt) =>
        new(
            IsRefreshActive: false,
            LatestAttemptedAtUtc: succeededAt,
            LatestSucceededAtUtc: succeededAt,
            ProviderRefreshOutcome.Succeeded,
            Version: 2);

    private static ProviderRefreshStatus FailedStatus() =>
        new(
            IsRefreshActive: false,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: null,
            ProviderRefreshOutcome.Failed,
            Version: 2);

    private static ProviderUsageSnapshot CreateSnapshot(
        ProviderKind provider,
        decimal? percentage = 50m,
        long? sessionTokens = 100,
        long? weeklyTokens = 1_000,
        DateTimeOffset? resetAt = null,
        DataAuthority authority = DataAuthority.LocalProviderReported,
        ProviderConnectionState connectionState = ProviderConnectionState.Connected,
        DateTimeOffset? capturedAt = null,
        IReadOnlyList<NormalizedQuotaWindow>? quotaWindows = null,
        IReadOnlyList<NormalizedActivityMetric>? activityMetrics = null,
        DateTimeOffset? sourceObservedAt = null) =>
        new(
            provider,
            connectionState,
            new PercentageUsageMetric(percentage, authority, DataFreshness.Live),
            resetAt ?? DateTimeOffset.UtcNow.AddHours(1),
            new TokenCountMetric(sessionTokens, authority, DataFreshness.Live),
            new TokenCountMetric(weeklyTokens, authority, DataFreshness.Live),
            capturedAt ?? DateTimeOffset.UtcNow,
            quotaWindows,
            activityMetrics,
            sourceObservedAt);

    private static NormalizedQuotaWindow QuotaWindow(
        string windowId,
        string? displayLabel,
        decimal usedPercentage,
        DateTimeOffset? resetAt = null,
        bool hasReset = true,
        DateTimeOffset? capturedAt = null,
        UsageMetricLabelOrigin labelOrigin = UsageMetricLabelOrigin.TokenFishDurationMapping) =>
        new(
            ProviderKind.Codex,
            windowId,
            displayLabel,
            displayLabel is null ? UsageMetricLabelOrigin.Unknown : labelOrigin,
            usedPercentage,
            hasReset ? resetAt ?? new DateTimeOffset(2026, 7, 19, 22, 0, 0, TimeSpan.Zero) : null,
            TimeSpan.FromDays(7),
            UsageMetricAvailability.Available,
            capturedAt ?? new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            DataAuthority.LocalProviderReported,
            DataFreshness.Live,
            "account/rateLimits/read");

    private static NormalizedQuotaWindow ClaudeQuotaWindow(
        string windowId,
        string displayLabel,
        decimal usedPercentage,
        DateTimeOffset? capturedAt = null) =>
        new(
            ProviderKind.Claude,
            windowId,
            displayLabel,
            UsageMetricLabelOrigin.TokenFishDurationMapping,
            usedPercentage,
            new DateTimeOffset(2026, 7, 19, 22, 0, 0, TimeSpan.Zero),
            displayLabel == "5h" ? TimeSpan.FromMinutes(300) : TimeSpan.FromMinutes(10_080),
            UsageMetricAvailability.Available,
            capturedAt ?? new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            DataAuthority.LocalProviderReported,
            DataFreshness.Live,
            "claude-status-line-bridge");

    private static NormalizedActivityMetric ActivityMetric(
        long value,
        DateOnly? start = null,
        DateOnly? inclusiveEnd = null,
        DateTimeOffset? capturedAt = null)
    {
        start ??= new DateOnly(2026, 7, 6);
        inclusiveEnd ??= new DateOnly(2026, 7, 12);
        var intervalStart = new DateTimeOffset(start.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var intervalEnd = new DateTimeOffset(inclusiveEnd.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        return new NormalizedActivityMetric(
            ProviderKind.Codex,
            "codex:activity:latest-seven-utc-dates:tokens",
            "Latest 7 UTC dates",
            UsageMetricLabelOrigin.TokenFishDurationMapping,
            value,
            UsageActivityUnit.Tokens,
            intervalStart,
            intervalEnd,
            UsageMetricAvailability.Available,
            capturedAt ?? new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            DataAuthority.TokenFishDerived,
            DataFreshness.Live,
            "account/usage/read");
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider()
            : this(new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero))
        {
        }

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
    }
}
