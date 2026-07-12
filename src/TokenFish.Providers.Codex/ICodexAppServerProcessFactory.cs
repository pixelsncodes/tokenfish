namespace TokenFish.Providers.Codex;

internal interface ICodexAppServerProcessFactory
{
    ICodexAppServerProcess Start(CodexAppServerLaunchCommand launchCommand);
}
