using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Providers.Codex;

public sealed class CodexSessionUsageCollector : IProviderUsageCollector, IAsyncDisposable
{
    private readonly ICodexAppServerSessionFactory _sessionFactory;
    private readonly CodexAppServerLaunchCommand _launchCommand;
    private readonly string _clientVersion;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ICodexAppServerSession? _session;
    private bool _disposeStarted;
    private bool _disposed;

    public CodexSessionUsageCollector(
        ICodexAppServerSessionFactory sessionFactory,
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _launchCommand = launchCommand ?? throw new ArgumentNullException(nameof(launchCommand));
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);

        _clientVersion = clientVersion;
    }

    public ProviderKind Provider => ProviderKind.Codex;

    public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        if (_disposeStarted)
        {
            throw new ObjectDisposedException(nameof(CodexSessionUsageCollector));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposeStarted)
            {
                throw new ObjectDisposedException(nameof(CodexSessionUsageCollector));
            }

            var session = _session;
            if (session is null)
            {
                session = await _sessionFactory.StartAsync(
                    _launchCommand,
                    _clientVersion,
                    cancellationToken).ConfigureAwait(false);
                _session = session;
            }

            return await session.CollectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposeStarted = true;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            if (_session is not null)
            {
                await _session.DisposeAsync().ConfigureAwait(false);
            }

            _disposed = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
