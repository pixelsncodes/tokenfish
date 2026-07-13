using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public interface IProviderRefreshLifecycle : IAsyncDisposable
{
    ProviderRefreshStatus RefreshStatus { get; }

    event Action<ProviderRefreshStatus>? RefreshStatusChanged;

    Task Completion { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ProviderUsageSnapshot>> RefreshAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
