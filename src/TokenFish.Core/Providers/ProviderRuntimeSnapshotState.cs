using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public sealed record ProviderRuntimeSnapshotState(
    ProviderUsageSnapshot Snapshot,
    DateTimeOffset AcceptedAt,
    DataFreshness EffectiveFreshness,
    ProviderCollectionOutcome CollectionOutcome,
    ProviderCollectionFailureReason? CollectionFailureReason);
