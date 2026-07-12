using System.Collections.ObjectModel;

namespace TokenFish.Providers.Codex;

public sealed class CodexAppServerLaunchCommand
{
    private CodexAppServerLaunchCommand(string executable, IReadOnlyList<string> arguments)
    {
        Executable = executable;
        Arguments = arguments;
    }

    public string Executable { get; }

    public IReadOnlyList<string> Arguments { get; }

    public static CodexAppServerLaunchCommand CreateNative(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        return new CodexAppServerLaunchCommand(
            executable,
            CreateArguments(["app-server", "--stdio"]));
    }

    public static CodexAppServerLaunchCommand CreateWsl(string? distributionName = null)
    {
        if (distributionName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(distributionName);
        }

        return new CodexAppServerLaunchCommand(
            "wsl.exe",
            distributionName is null
                ? CreateArguments(["--exec", "codex", "app-server", "--stdio"])
                : CreateArguments(
                [
                    "--distribution",
                    distributionName,
                    "--exec",
                    "codex",
                    "app-server",
                    "--stdio"
                ]));
    }

    private static ReadOnlyCollection<string> CreateArguments(string[] arguments) =>
        Array.AsReadOnly(arguments);
}
