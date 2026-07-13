using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public sealed class OnboardingVerificationPresenter
{
    public OnboardingVerificationDisplayState Present(OnboardingReadinessResult result, bool isRechecking = false, bool hasError = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        var providers = result.Providers.Select(PresentProvider).ToArray();
        return new OnboardingVerificationDisplayState(result.State, providers, isRechecking, hasError,
            isRechecking ? "Checking provider readiness." : hasError ? "Recheck could not be completed. Try again." :
            $"Readiness updated: {result.State}.");
    }

    private static OnboardingProviderVerificationDisplayState PresentProvider(OnboardingProviderReadiness provider)
    {
        var (heading, description) = provider.State switch
        {
            OnboardingReadinessState.Ready => ("Ready", "Usage data is available."),
            OnboardingReadinessState.Waiting when provider.Reason == OnboardingReadinessReason.AwaitingFirstObservation && provider.Provider == TokenFish.Core.Models.ProviderKind.Claude =>
                ("Waiting", "Waiting for a normal Claude Code status-line update through the local bridge."),
            OnboardingReadinessState.Waiting when provider.Reason == OnboardingReadinessReason.AwaitingFirstObservation =>
                ("Waiting", "Waiting for the first successful collection from the configured Codex runtime."),
            OnboardingReadinessState.Waiting => ("Waiting", "Provider state was observed, but usable usage data is not available yet."),
            _ => ("Problem", FailureDescription(provider.Provider, provider.CollectionFailureReason))
        };
        return new(provider.Provider, provider.State, heading, description, provider.CollectionFailureReason);
    }

    private static string FailureDescription(TokenFish.Core.Models.ProviderKind provider, ProviderCollectionFailureReason? reason) => reason switch
    {
        ProviderCollectionFailureReason.ClaudeBridgeMalformed => "TokenFish could not read valid Claude bridge data. Check the manually configured status-line snippet.",
        ProviderCollectionFailureReason.ClaudeBridgeUnreadable => "TokenFish could not read the Claude bridge state. Try using Claude Code normally, then recheck.",
        ProviderCollectionFailureReason.CodexSession => "The configured Codex runtime could not start a usable session.",
        ProviderCollectionFailureReason.CodexProtocol => "The configured Codex runtime did not return a usable response.",
        ProviderCollectionFailureReason.CodexRateLimitsResponse or ProviderCollectionFailureReason.CodexAccountUsageResponse or ProviderCollectionFailureReason.CodexUsageNormalization => "The configured Codex runtime returned usage data TokenFish could not use.",
        _ when provider == TokenFish.Core.Models.ProviderKind.Claude => "Claude usage is currently unavailable. Recheck after a normal Claude Code update.",
        _ => "Codex usage is currently unavailable. Recheck the configured runtime."
    };
}

public sealed record OnboardingVerificationDisplayState(
    OnboardingReadinessState OverallState,
    IReadOnlyList<OnboardingProviderVerificationDisplayState> Providers,
    bool IsRechecking,
    bool HasRecheckError,
    string Announcement);

public sealed record OnboardingProviderVerificationDisplayState(
    TokenFish.Core.Models.ProviderKind Provider,
    OnboardingReadinessState State,
    string Heading,
    string Description,
    ProviderCollectionFailureReason? FailureReason);
