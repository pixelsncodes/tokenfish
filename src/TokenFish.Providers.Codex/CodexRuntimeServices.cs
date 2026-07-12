using TokenFish.Core.Providers;

namespace TokenFish.Providers.Codex;

public sealed class CodexRuntimeServices : IAsyncDisposable
{
    private readonly CodexSessionUsageCollector _codexUsageCollector;
    private readonly IProviderUsageCollector _safeCodexUsageCollector;

    private bool _disposed;

    private CodexRuntimeServices(
        CodexSessionUsageCollector codexUsageCollector,
        IProviderUsageCollector safeCodexUsageCollector)
    {
        _codexUsageCollector = codexUsageCollector;
        _safeCodexUsageCollector = safeCodexUsageCollector;
    }

    public IProviderUsageCollector CodexUsageCollector
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _safeCodexUsageCollector;
        }
    }

    public static CodexRuntimeServices Create(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);

        var processFactory = new CodexAppServerProcessFactory();
        var sessionFactory = new CodexAppServerSessionFactory(processFactory);

        return Create(launchCommand, clientVersion, sessionFactory);
    }

    internal static CodexRuntimeServices Create(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion,
        ICodexAppServerSessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
        ArgumentNullException.ThrowIfNull(sessionFactory);

        var runtimeCollector = new CodexSessionUsageCollector(
            sessionFactory,
            launchCommand,
            clientVersion);
        var safeCollector = new CodexRuntimeFailureMappingCollector(runtimeCollector);

        return new CodexRuntimeServices(runtimeCollector, safeCollector);
    }

    internal static CodexRuntimeServices Create(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion,
        ICodexAppServerProcessFactory processFactory,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(processFactory);

        ArgumentNullException.ThrowIfNull(launchCommand);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);

        var runtimeCollector = new CodexSessionUsageCollector(
            new CodexAppServerSessionFactory(processFactory, timeProvider),
            launchCommand,
            clientVersion);
        var safeCollector = new CodexRuntimeFailureMappingCollector(
            runtimeCollector,
            timeProvider);

        return new CodexRuntimeServices(runtimeCollector, safeCollector);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _codexUsageCollector.DisposeAsync().ConfigureAwait(false);
    }
}
