using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public interface IProviderRuntimeSnapshotStore
{
    void Store(IReadOnlyList<ProviderUsageSnapshot> snapshots);

    bool TryGetCurrent(
        ProviderKind provider,
        out ProviderRuntimeSnapshotState currentState);

    IReadOnlyDictionary<ProviderKind, ProviderRuntimeSnapshotState> GetCurrentSnapshots();
}
