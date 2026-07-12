using TokenFish.Core.Providers;

namespace TokenFish.Providers.Codex;

public sealed class CodexRuntimeServices : IAsyncDisposable
{
    private readonly CodexSessionUsageCollector _codexUsageCollector;

    private bool _disposed;

    private CodexRuntimeServices(CodexSessionUsageCollector codexUsageCollector)
    {
        _codexUsageCollector = codexUsageCollector;
    }

    public IProviderUsageCollector CodexUsageCollector
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _codexUsageCollector;
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

        return new CodexRuntimeServices(
            new CodexSessionUsageCollector(
                sessionFactory,
                launchCommand,
                clientVersion));
    }

    internal static CodexRuntimeServices Create(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion,
        ICodexAppServerProcessFactory processFactory,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(processFactory);

        return Create(
            launchCommand,
            clientVersion,
            new CodexAppServerSessionFactory(processFactory, timeProvider));
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
