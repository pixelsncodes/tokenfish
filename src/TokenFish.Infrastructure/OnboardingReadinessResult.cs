using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public sealed record OnboardingReadinessResult(
    OnboardingReadinessState State,
    IReadOnlyList<OnboardingProviderReadiness> Providers);

public sealed record OnboardingProviderReadiness(
    ProviderKind Provider,
    OnboardingReadinessState State,
    OnboardingReadinessReason Reason,
    ProviderCollectionFailureReason? CollectionFailureReason);

public enum OnboardingReadinessState
{
    Ready,
    Waiting,
    Problem
}

public enum OnboardingReadinessReason
{
    ProviderReady,
    AwaitingFirstObservation,
    AwaitingUsableData,
    CollectionFailed
}
