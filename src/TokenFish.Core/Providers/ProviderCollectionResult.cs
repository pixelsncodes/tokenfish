using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public sealed record ProviderCollectionResult
{
    public ProviderCollectionResult(
        ProviderUsageSnapshot snapshot,
        ProviderCollectionOutcome outcome,
        ProviderCollectionFailureReason? failureReason = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Outcome = outcome;
        FailureReason = failureReason;

        if (outcome is ProviderCollectionOutcome.Failed && failureReason is null)
        {
            throw new ArgumentException(
                "A failed provider collection result must have a failure reason.",
                nameof(FailureReason));
        }

        if (outcome is not ProviderCollectionOutcome.Failed && failureReason is not null)
        {
            throw new ArgumentException(
                "Only failed provider collection results can have a failure reason.",
                nameof(FailureReason));
        }
    }

    public ProviderUsageSnapshot Snapshot { get; }

    public ProviderCollectionOutcome Outcome { get; }

    public ProviderCollectionFailureReason? FailureReason { get; }
}
