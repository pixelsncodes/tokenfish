using TokenFish.Core.Models;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class PopupDisplayStateUpdatePlannerTests
{
    [Fact]
    public void FirstStateRebuildsProviderCards()
    {
        var planner = new PopupDisplayStateUpdatePlanner();

        var plan = planner.Plan(CreateState());

        Assert.True(plan.RebuildProviderCards);
        Assert.True(plan.AffectsLayout);
    }

    [Fact]
    public void RepeatedIdenticalDisplayStateDoesNotRebuildOrReassignProgress()
    {
        var planner = new PopupDisplayStateUpdatePlanner();
        var state = CreateState();
        _ = planner.Plan(state);

        var plan = planner.Plan(state);

        Assert.False(plan.RebuildProviderCards);
        Assert.False(plan.AffectsLayout);
        Assert.Empty(plan.ProgressValueUpdates);
    }

    [Fact]
    public void TimerOnlyFooterUpdateDoesNotRebuildQuotaRowsOrReassignProgress()
    {
        var planner = new PopupDisplayStateUpdatePlanner();
        _ = planner.Plan(CreateState(footerText: "Updated just now · Reported by Codex"));

        var plan = planner.Plan(CreateState(footerText: "Updated 1 min ago · Reported by Codex"));

        Assert.False(plan.RebuildProviderCards);
        Assert.False(plan.AffectsLayout);
        Assert.Empty(plan.ProgressValueUpdates);
    }

    [Fact]
    public void TimerOnlyRefreshHealthUpdateDoesNotRebuildProviderCardsOrReassignProgress()
    {
        var planner = new PopupDisplayStateUpdatePlanner();
        _ = planner.Plan(CreateState(statusText: "Updated just now"));

        var plan = planner.Plan(CreateState(statusText: "Updated 1 min ago"));

        Assert.False(plan.RebuildProviderCards);
        Assert.True(plan.AffectsLayout);
        Assert.Empty(plan.ProgressValueUpdates);
    }

    [Fact]
    public void RefreshCommandStateUpdateDoesNotRebuildProviderCardsOrReassignProgress()
    {
        var planner = new PopupDisplayStateUpdatePlanner();
        _ = planner.Plan(CreateState(
            refreshCommandState: ManualRefreshCommandState.Available));

        var plan = planner.Plan(CreateState(
            refreshCommandState: ManualRefreshCommandState.Refreshing));

        Assert.False(plan.RebuildProviderCards);
        Assert.True(plan.AffectsLayout);
        Assert.Empty(plan.ProgressValueUpdates);
    }

    [Fact]
    public void ChangedUsagePercentageUpdatesExistingProgressControl()
    {
        var planner = new PopupDisplayStateUpdatePlanner();
        _ = planner.Plan(CreateState(progressValue: 29m));

        var plan = planner.Plan(CreateState(progressValue: 31m));

        Assert.False(plan.RebuildProviderCards);
        var update = Assert.Single(plan.ProgressValueUpdates);
        Assert.Equal(0, update.ProviderIndex);
        Assert.Equal(0, update.QuotaWindowIndex);
        Assert.Equal(31m, update.ProgressValue);
    }

    [Fact]
    public void ActivityAppearingRequiresLayoutRebuild()
    {
        var planner = new PopupDisplayStateUpdatePlanner();
        _ = planner.Plan(CreateState(activityRows: []));

        var plan = planner.Plan(CreateState(activityRows: [CreateActivity()]));

        Assert.True(plan.RebuildProviderCards);
        Assert.True(plan.AffectsLayout);
    }

    [Fact]
    public void MultipleQuotaWindowsCanUpdateProgressIndependently()
    {
        var planner = new PopupDisplayStateUpdatePlanner();
        _ = planner.Plan(CreateState(
            quotaWindows:
            [
                CreateQuotaWindow("Weekly usage", 29m),
                CreateQuotaWindow("Daily usage", 40m)
            ]));

        var plan = planner.Plan(CreateState(
            quotaWindows:
            [
                CreateQuotaWindow("Weekly usage", 29m),
                CreateQuotaWindow("Daily usage", 41m)
            ]));

        var update = Assert.Single(plan.ProgressValueUpdates);
        Assert.Equal(1, update.QuotaWindowIndex);
        Assert.Equal(41m, update.ProgressValue);
    }

    [Fact]
    public void ProgressPlansRemainValidWhenReducedMotionIsPreferred()
    {
        var planner = new PopupDisplayStateUpdatePlanner();
        _ = planner.Plan(CreateState(progressValue: 10m));

        var plan = planner.Plan(CreateState(progressValue: 11m));

        Assert.False(plan.RebuildProviderCards);
        Assert.Equal(11m, Assert.Single(plan.ProgressValueUpdates).ProgressValue);
    }

    private static TrayPopupDisplayState CreateState(
        decimal progressValue = 29m,
        string footerText = "Updated just now · Reported by Codex",
        string statusText = "Running",
        IReadOnlyList<PopupQuotaWindowDisplayState>? quotaWindows = null,
        IReadOnlyList<PopupActivityDisplayState>? activityRows = null,
        ManualRefreshCommandState? refreshCommandState = null) =>
        new(
            PopupApplicationDisplayState.Running,
            statusText,
            [
                new ProviderCardDisplayState(
                    ProviderKind.Codex,
                    "Codex",
                    "Connected",
                    quotaWindows ?? [CreateQuotaWindow("Weekly usage", progressValue)],
                    activityRows ?? [CreateActivity()],
                    footerText: footerText)
            ],
            refreshCommandState ?? ManualRefreshCommandState.Available);

    private static PopupQuotaWindowDisplayState CreateQuotaWindow(
        string label,
        decimal progressValue) =>
        new(
            label,
            $"{progressValue}% used",
            progressValue,
            $"{label}: {progressValue}% used",
            "Resets in 6d 19h",
            "Sunday, July 19 at 12:02 PM");

    private static PopupActivityDisplayState CreateActivity() =>
        new(
            "Tokens used",
            "Jul 6-12",
            "105.7M",
            "105,740,013 tokens used from July 6 through July 12");
}
