using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class TrayPopupDisplayStateAdapterTests
{
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

        Assert.Equal([ProviderKind.Claude, ProviderKind.Codex], state.Providers.Select(provider => provider.Provider));
    }

    [Fact]
    public void NoSnapshotMapsToUnavailableFieldsRatherThanZeroValues()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            []);
        var provider = Assert.Single(state.Providers);

        Assert.Null(provider.UsagePercentage);
        Assert.Equal("Unavailable", provider.UsageWindow);
        Assert.Equal("Unavailable", provider.SessionTokens);
        Assert.Equal("Unavailable", provider.WeeklyTokens);
        Assert.Equal("Unavailable", provider.Reset);
    }

    [Fact]
    public void AvailablePercentageIsPreserved()
    {
        var provider = SingleProvider(CreateSnapshot(ProviderKind.Codex, percentage: 42.5m));

        Assert.Equal(42.5m, provider.UsagePercentage);
        Assert.Equal("42.5%", provider.UsageWindow);
    }

    [Fact]
    public void UnavailablePercentageDoesNotCreateFabricatedProgressValue()
    {
        var provider = SingleProvider(CreateSnapshot(ProviderKind.Codex, percentage: null));

        Assert.Null(provider.UsagePercentage);
        Assert.Equal("Unavailable", provider.UsageWindow);
    }

    [Fact]
    public void SessionAndWeeklyTokenAvailabilityAreHandledIndependently()
    {
        var provider = SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            sessionTokens: null,
            weeklyTokens: 1_234));

        Assert.Equal("Unavailable", provider.SessionTokens);
        Assert.Equal("1,234", provider.WeeklyTokens);
    }

    [Fact]
    public void ResetCountdownUsesInjectedTime()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var provider = SingleProvider(
            CreateSnapshot(
                ProviderKind.Codex,
                resetAt: new DateTimeOffset(2026, 7, 12, 9, 30, 0, TimeSpan.Zero)),
            timeProvider);

        Assert.Equal("1h 30m", provider.Reset);
    }

    [Fact]
    public void PastResetTimestampMapsToDueState()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero));
        var provider = SingleProvider(
            CreateSnapshot(
                ProviderKind.Codex,
                resetAt: new DateTimeOffset(2026, 7, 12, 7, 59, 0, TimeSpan.Zero)),
            timeProvider);

        Assert.Equal("Reset due", provider.Reset);
    }

    [Fact]
    public void FreshnessComesFromExistingStoreReadResult()
    {
        var timeProvider = new ManualTimeProvider();
        var store = new InMemoryProviderRuntimeSnapshotStore(timeProvider, TimeSpan.FromMinutes(5));
        store.Store([CreateSnapshot(ProviderKind.Codex)]);
        timeProvider.Advance(TimeSpan.FromMinutes(6));

        var state = new TrayPopupDisplayStateAdapter(timeProvider).Create(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            ApplicationRuntimeStatus.Running,
            store);

        Assert.Equal("Stale", Assert.Single(state.Providers).Freshness);
    }

    [Fact]
    public void DataAuthorityLabelsMapCorrectly()
    {
        Assert.Equal("Provider-reported", SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            authority: DataAuthority.LocalProviderReported)).DataAuthority);
        Assert.Equal("Locally calculated", SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            authority: DataAuthority.TokenFishDerived)).DataAuthority);
        Assert.Equal("Estimated", SingleProvider(CreateSnapshot(
            ProviderKind.Codex,
            authority: DataAuthority.UserControlled)).DataAuthority);
    }

    [Fact]
    public void ApplicationFaultsExposeOnlyFixedSafeUiText()
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [],
            ApplicationRuntimeStatus.RefreshFaulted);

        Assert.Equal(PopupApplicationDisplayState.RefreshIssue, state.ApplicationState);
        Assert.Equal("TokenFish could not refresh usage", state.StatusText);
        Assert.DoesNotContain("exception", state.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderCardDisplayState SingleProvider(
        ProviderUsageSnapshot snapshot,
        TimeProvider? timeProvider = null)
    {
        var state = CreateState(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            [snapshot],
            ApplicationRuntimeStatus.Running,
            timeProvider);

        return Assert.Single(state.Providers);
    }

    private static TrayPopupDisplayState CreateState(
        AppSettings settings,
        IReadOnlyList<ProviderUsageSnapshot> snapshots,
        ApplicationRuntimeStatus? status = null,
        TimeProvider? timeProvider = null)
    {
        timeProvider ??= new ManualTimeProvider();
        var store = new InMemoryProviderRuntimeSnapshotStore(timeProvider, TimeSpan.FromMinutes(5));
        if (snapshots.Count > 0)
        {
            store.Store(snapshots);
        }

        return new TrayPopupDisplayStateAdapter(timeProvider).Create(
            settings,
            status ?? ApplicationRuntimeStatus.Running,
            store);
    }

    private static ProviderUsageSnapshot CreateSnapshot(
        ProviderKind provider,
        decimal? percentage = 50m,
        long? sessionTokens = 100,
        long? weeklyTokens = 1_000,
        DateTimeOffset? resetAt = null,
        DataAuthority authority = DataAuthority.LocalProviderReported) =>
        new(
            provider,
            ProviderConnectionState.Connected,
            new PercentageUsageMetric(percentage, authority, DataFreshness.Live),
            resetAt ?? DateTimeOffset.UtcNow.AddHours(1),
            new TokenCountMetric(sessionTokens, authority, DataFreshness.Live),
            new TokenCountMetric(weeklyTokens, authority, DataFreshness.Live),
            DateTimeOffset.UtcNow);

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
