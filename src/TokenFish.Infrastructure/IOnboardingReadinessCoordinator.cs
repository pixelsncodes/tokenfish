using TokenFish.Core.Models;

namespace TokenFish.Infrastructure;

public interface IOnboardingReadinessCoordinator
{
    OnboardingReadinessResult Evaluate(AppSettings settings);

    Task<OnboardingReadinessResult> RecheckAsync(
        AppSettings settings,
        CancellationToken cancellationToken);
}
