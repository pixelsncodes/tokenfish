using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class OnboardingVerificationPresenterTests
{
    [Fact]
    public void MapsSelectedProvidersInCoordinatorOrderWithoutRawFailureText()
    {
        var state = new OnboardingVerificationPresenter().Present(new OnboardingReadinessResult(
            OnboardingReadinessState.Problem,
            [new(ProviderKind.Claude, OnboardingReadinessState.Problem, OnboardingReadinessReason.CollectionFailed, ProviderCollectionFailureReason.ClaudeBridgeMalformed)]));
        var provider = Assert.Single(state.Providers);
        Assert.Equal("Problem", provider.Heading);
        Assert.Contains("bridge", provider.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClaudeBridgeMalformed", provider.Description);
    }

    [Theory]
    [InlineData(ProviderCollectionFailureReason.CodexSession)]
    [InlineData(ProviderCollectionFailureReason.CodexProtocol)]
    [InlineData(ProviderCollectionFailureReason.CodexRateLimitsResponse)]
    [InlineData(ProviderCollectionFailureReason.CodexAccountUsageResponse)]
    [InlineData(ProviderCollectionFailureReason.CodexUsageNormalization)]
    public void MapsEveryCodexFailureSafely(ProviderCollectionFailureReason reason)
    {
        var state = new OnboardingVerificationPresenter().Present(new OnboardingReadinessResult(OnboardingReadinessState.Problem,
            [new(ProviderKind.Codex, OnboardingReadinessState.Problem, OnboardingReadinessReason.CollectionFailed, reason)]));
        Assert.DoesNotContain(reason.ToString(), Assert.Single(state.Providers).Description);
    }
}
