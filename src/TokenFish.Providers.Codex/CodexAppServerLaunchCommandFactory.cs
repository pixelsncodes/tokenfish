using TokenFish.Core.Models;

namespace TokenFish.Providers.Codex;

public static class CodexAppServerLaunchCommandFactory
{
    private const string NativeExecutable = "codex.exe";

    public static CodexAppServerLaunchCommand Create(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var distributionName = NormalizeDistributionName(settings.CodexWslDistributionName);

        return settings.CodexRuntimeMode switch
        {
            CodexRuntimeMode.NativeWindows => CreateNative(distributionName),
            CodexRuntimeMode.Wsl => CodexAppServerLaunchCommand.CreateWsl(distributionName),
            CodexRuntimeMode.WslLoginShell =>
                CodexAppServerLaunchCommand.CreateWslLoginShell(distributionName),
            _ => throw new CodexRuntimeConfigurationException(
                "Codex runtime mode is not supported.")
        };
    }

    private static CodexAppServerLaunchCommand CreateNative(string? distributionName)
    {
        if (distributionName is not null)
        {
            throw new CodexRuntimeConfigurationException(
                "Codex WSL distribution cannot be used with native runtime mode.");
        }

        return CodexAppServerLaunchCommand.CreateNative(NativeExecutable);
    }

    private static string? NormalizeDistributionName(string? distributionName) =>
        string.IsNullOrWhiteSpace(distributionName) ? null : distributionName;
}
