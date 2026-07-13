using TokenFish.Core.Models;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class OnboardingFlowControllerTests
{
    [Fact]
    public void BeginsAtWelcome() => Assert.Equal(OnboardingStep.Welcome, Create().Step);

    [Fact]
    public void RequiresAProviderBeforeContinuing()
    {
        var flow = Create();
        flow.Continue();
        flow.SetProviderSelection(false, false);

        Assert.False(flow.Continue());
        Assert.Equal(OnboardingStep.ProviderSelection, flow.Step);
    }

    [Theory]
    [InlineData(true, false, ProviderSelectionMode.ClaudeOnly)]
    [InlineData(false, true, ProviderSelectionMode.CodexOnly)]
    [InlineData(true, true, ProviderSelectionMode.Both)]
    public void ProviderSelectionMapsToSettings(bool claude, bool codex, ProviderSelectionMode expected)
    {
        var flow = Create();
        flow.SetProviderSelection(claude, codex);

        Assert.Equal(expected, flow.CreatePendingSettings().ProviderSelectionMode);
        Assert.False(flow.CreatePendingSettings().IsOnboardingCompleted);
    }

    [Fact]
    public void SkipsUnselectedProviderStepsAndBackTracksThem()
    {
        var flow = Create();
        flow.Continue();
        flow.SetProviderSelection(true, false);
        flow.Continue();
        Assert.Equal(OnboardingStep.ClaudeBridge, flow.Step);
        flow.Continue();
        flow.Back();
        Assert.Equal(OnboardingStep.ClaudeBridge, flow.Step);
    }

    [Fact]
    public void NativeWindowsOmitsDistributionAndWslTrimsIt()
    {
        var flow = Create();
        flow.SetRuntimeMode(CodexRuntimeMode.NativeWindows);
        flow.SetWslDistributionName(" Ubuntu ");
        Assert.False(flow.IsWslDistributionEnabled);
        Assert.Null(flow.CreatePendingSettings().CodexWslDistributionName);

        flow.SetRuntimeMode(CodexRuntimeMode.Wsl);
        Assert.True(flow.IsWslDistributionEnabled);
        Assert.Equal("Ubuntu", flow.CreatePendingSettings().CodexWslDistributionName);
    }

    private static OnboardingFlowController Create() => new(new AppSettings());
}
