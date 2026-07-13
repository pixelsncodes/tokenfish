using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public interface IApplicationRuntimeHost
{
    ApplicationRuntimeStatus Status { get; }

    ProviderRefreshStatus RefreshStatus { get; }

    TokenFish.Core.Models.AppSettings? CurrentSettings { get; }

    event Action<ApplicationRuntimeStatus>? StatusChanged;

    event Action<ProviderRefreshStatus>? RefreshStatusChanged;

    Task StartAsync(CancellationToken cancellationToken);

    Task RefreshAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    void ReportShellFault();
}
