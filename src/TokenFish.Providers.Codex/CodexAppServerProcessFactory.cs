using System.Diagnostics;

namespace TokenFish.Providers.Codex;

internal sealed class CodexAppServerProcessFactory : ICodexAppServerProcessFactory
{
    public ICodexAppServerProcess Start(CodexAppServerLaunchCommand launchCommand)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);

        var process = new Process
        {
            StartInfo = CreateStartInfo(launchCommand),
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Codex app server process could not be started.");
            }

            return new SystemCodexAppServerProcess(process);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    internal static ProcessStartInfo CreateStartInfo(CodexAppServerLaunchCommand launchCommand)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);

        var startInfo = new ProcessStartInfo
        {
            FileName = launchCommand.Executable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in launchCommand.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
