using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Providers.Codex;

namespace TokenFish.Infrastructure;

public sealed class TokenFishApplicationServices : IAsyncDisposable
{
    private static readonly TimeSpan DefaultProviderRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly ProviderRefreshLifecycle _providerRefreshLifecycle;
    private readonly IAsyncDisposable? _codexRuntimeOwner;

    private bool _disposed;

    private TokenFishApplicationServices(
        ProviderRefreshLifecycle providerRefreshLifecycle,
        IProviderUsageCollector? codexUsageCollector,
        IAsyncDisposable? codexRuntimeOwner)
    {
        _providerRefreshLifecycle = providerRefreshLifecycle;
        CodexUsageCollector = codexUsageCollector;
        _codexRuntimeOwner = codexRuntimeOwner;
    }

    public IProviderUsageCollector? CodexUsageCollector { get; }

    public ProviderRefreshLifecycle ProviderRefreshLifecycle => _providerRefreshLifecycle;

    public static TokenFishApplicationServices Create(
        AppSettings settings,
        string clientVersion)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);

        return Create(
            settings,
            clientVersion,
            DefaultProviderRefreshInterval,
            TimeProvider.System,
            static (launchCommand, version) =>
            {
                var services = CodexRuntimeServices.Create(launchCommand, version);
                return new ProviderRuntime(services.CodexUsageCollector, services);
            });
    }

    internal static TokenFishApplicationServices Create(
        AppSettings settings,
        string clientVersion,
        Func<CodexAppServerLaunchCommand, string, ProviderRuntime> createCodexRuntime) =>
        Create(
            settings,
            clientVersion,
            DefaultProviderRefreshInterval,
            TimeProvider.System,
            createCodexRuntime);

    internal static TokenFishApplicationServices Create(
        AppSettings settings,
        string clientVersion,
        TimeSpan providerRefreshInterval,
        TimeProvider timeProvider,
        Func<CodexAppServerLaunchCommand, string, ProviderRuntime> createCodexRuntime)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(createCodexRuntime);

        var collectors = new List<IProviderUsageCollector>();
        IProviderUsageCollector? codexUsageCollector = null;
        IAsyncDisposable? codexRuntimeOwner = null;

        if (!IsCodexEnabled(settings.ProviderSelectionMode))
        {
            return CreateServices(settings, providerRefreshInterval, timeProvider, collectors, null, null);
        }

        var launchCommand = CodexAppServerLaunchCommandFactory.Create(settings);
        var codexRuntime = createCodexRuntime(launchCommand, clientVersion);
        codexUsageCollector = codexRuntime.Collector;
        codexRuntimeOwner = codexRuntime.Owner;
        collectors.Add(codexUsageCollector);

        return CreateServices(
            settings,
            providerRefreshInterval,
            timeProvider,
            collectors,
            codexUsageCollector,
            codexRuntimeOwner);
    }

    private static TokenFishApplicationServices CreateServices(
        AppSettings settings,
        TimeSpan providerRefreshInterval,
        TimeProvider timeProvider,
        IEnumerable<IProviderUsageCollector> collectors,
        IProviderUsageCollector? codexUsageCollector,
        IAsyncDisposable? codexRuntimeOwner)
    {
        var coordinator = new ProviderUsageCollectionCoordinator(collectors);
        var providerRefreshLifecycle = new ProviderRefreshLifecycle(
            coordinator,
            settings,
            providerRefreshInterval,
            timeProvider);

        return new TokenFishApplicationServices(
            providerRefreshLifecycle,
            codexUsageCollector,
            codexRuntimeOwner);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _providerRefreshLifecycle.DisposeAsync().ConfigureAwait(false);

        if (_codexRuntimeOwner is not null)
        {
            await _codexRuntimeOwner.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool IsCodexEnabled(ProviderSelectionMode providerSelectionMode) =>
        providerSelectionMode switch
        {
            ProviderSelectionMode.CodexOnly or ProviderSelectionMode.Both => true,
            ProviderSelectionMode.ClaudeOnly => false,
            _ => throw new ArgumentOutOfRangeException(
                nameof(providerSelectionMode),
                "Provider selection mode is not supported.")
        };

    internal sealed record ProviderRuntime(
        IProviderUsageCollector Collector,
        IAsyncDisposable Owner);
}
