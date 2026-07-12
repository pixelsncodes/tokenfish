namespace TokenFish.Providers.Codex;

public sealed class CodexAppServerSessionFactory : ICodexAppServerSessionFactory
{
    private readonly ICodexAppServerProcessFactory _processFactory;
    private readonly TimeProvider? _timeProvider;

    public CodexAppServerSessionFactory(TimeProvider? timeProvider = null)
        : this(new CodexAppServerProcessFactory(), timeProvider)
    {
    }

    internal CodexAppServerSessionFactory(
        ICodexAppServerProcessFactory processFactory,
        TimeProvider? timeProvider = null)
    {
        _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        _timeProvider = timeProvider;
    }

    public async Task<ICodexAppServerSession> StartAsync(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion,
        CancellationToken cancellationToken) =>
        await CodexAppServerSession.StartAsync(
            launchCommand,
            clientVersion,
            _timeProvider,
            _processFactory,
            cancellationToken).ConfigureAwait(false);
}
