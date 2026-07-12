namespace TokenFish.Infrastructure;

public interface IApplicationRuntimeHost
{
    ApplicationRuntimeStatus Status { get; }

    TokenFish.Core.Models.AppSettings? CurrentSettings { get; }

    event Action<ApplicationRuntimeStatus>? StatusChanged;

    Task StartAsync(CancellationToken cancellationToken);

    Task RefreshAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    void ReportShellFault();
}
