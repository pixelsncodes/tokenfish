using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public interface IProviderRefreshLifecycle : IAsyncDisposable
{
    Task Completion { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ProviderUsageSnapshot>> RefreshAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
