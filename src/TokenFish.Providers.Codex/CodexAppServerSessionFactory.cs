namespace TokenFish.Providers.Codex;

public sealed class CodexAppServerSessionFactory : ICodexAppServerSessionFactory
{
    private readonly TimeProvider? _timeProvider;

    public CodexAppServerSessionFactory(TimeProvider? timeProvider = null)
    {
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
            cancellationToken).ConfigureAwait(false);
}
