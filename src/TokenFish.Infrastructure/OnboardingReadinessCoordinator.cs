using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public sealed class OnboardingReadinessCoordinator : IOnboardingReadinessCoordinator
{
    private readonly IProviderRuntimeSnapshotStore _snapshotStore;
    private readonly IProviderRefreshLifecycle _refreshLifecycle;

    public OnboardingReadinessCoordinator(
        IProviderRuntimeSnapshotStore snapshotStore,
        IProviderRefreshLifecycle refreshLifecycle)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
        _refreshLifecycle = refreshLifecycle ?? throw new ArgumentNullException(nameof(refreshLifecycle));
    }

    public OnboardingReadinessResult Evaluate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var providers = GetSelectedProviders(settings.ProviderSelectionMode)
            .Select(EvaluateProvider)
            .ToArray();

        return new OnboardingReadinessResult(GetOverallState(providers), providers);
    }

    public async Task<OnboardingReadinessResult> RecheckAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _refreshLifecycle.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return Evaluate(settings);
    }

    private OnboardingProviderReadiness EvaluateProvider(ProviderKind provider)
    {
        if (!_snapshotStore.TryGetCurrent(provider, out var state))
        {
            return Waiting(provider, OnboardingReadinessReason.AwaitingFirstObservation);
        }

        return state.CollectionOutcome switch
        {
            ProviderCollectionOutcome.Failed => new OnboardingProviderReadiness(
                provider,
                OnboardingReadinessState.Problem,
                OnboardingReadinessReason.CollectionFailed,
                state.CollectionFailureReason),
            ProviderCollectionOutcome.NoObservation => Waiting(
                provider,
                OnboardingReadinessReason.AwaitingFirstObservation),
            ProviderCollectionOutcome.Succeeded when HasUsableData(state) => new OnboardingProviderReadiness(
                provider,
                OnboardingReadinessState.Ready,
                OnboardingReadinessReason.ProviderReady,
                null),
            _ => Waiting(provider, OnboardingReadinessReason.AwaitingUsableData)
        };
    }

    private static OnboardingProviderReadiness Waiting(
        ProviderKind provider,
        OnboardingReadinessReason reason) =>
        new(provider, OnboardingReadinessState.Waiting, reason, null);

    private static bool HasUsableData(ProviderRuntimeSnapshotState state) =>
        state.Snapshot.ConnectionState == ProviderConnectionState.Connected &&
        state.EffectiveFreshness is DataFreshness.Live or DataFreshness.Cached &&
        (state.Snapshot.UsageWindow.IsAvailable ||
         state.Snapshot.SessionTokens.IsAvailable ||
         state.Snapshot.WeeklyTokens.IsAvailable ||
         state.Snapshot.QuotaWindows.Any(window => window.IsAvailable) ||
         state.Snapshot.ActivityMetrics.Any(metric => metric.IsAvailable));

    private static OnboardingReadinessState GetOverallState(
        IReadOnlyList<OnboardingProviderReadiness> providers)
    {
        if (providers.Any(provider => provider.State == OnboardingReadinessState.Problem))
        {
            return OnboardingReadinessState.Problem;
        }

        return providers.All(provider => provider.State == OnboardingReadinessState.Ready)
            ? OnboardingReadinessState.Ready
            : OnboardingReadinessState.Waiting;
    }

    private static ProviderKind[] GetSelectedProviders(ProviderSelectionMode selectionMode) =>
        selectionMode switch
        {
            ProviderSelectionMode.ClaudeOnly => [ProviderKind.Claude],
            ProviderSelectionMode.CodexOnly => [ProviderKind.Codex],
            ProviderSelectionMode.Both => [ProviderKind.Claude, ProviderKind.Codex],
            _ => throw new ArgumentOutOfRangeException(nameof(selectionMode), selectionMode, null)
        };
}
