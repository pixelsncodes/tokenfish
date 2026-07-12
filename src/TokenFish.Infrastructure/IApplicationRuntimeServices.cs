using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public interface IApplicationRuntimeServices : IAsyncDisposable
{
    IProviderRefreshLifecycle ProviderRefreshLifecycle { get; }

    IProviderRuntimeSnapshotStore ProviderRuntimeSnapshotStore { get; }
}
