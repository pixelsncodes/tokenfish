namespace TokenFish.Providers.Codex;

public interface ICodexAppServerSessionFactory
{
    Task<ICodexAppServerSession> StartAsync(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion,
        CancellationToken cancellationToken);
}
