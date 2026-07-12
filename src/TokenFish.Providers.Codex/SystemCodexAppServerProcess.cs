using System.Diagnostics;

namespace TokenFish.Providers.Codex;

internal sealed class SystemCodexAppServerProcess(Process process) : ICodexAppServerProcess
{
    public TextWriter StandardInput => process.StandardInput;

    public TextReader StandardOutput => process.StandardOutput;

    public TextReader StandardError => process.StandardError;

    public bool HasExited => process.HasExited;

    public int? ExitCode => process.HasExited ? process.ExitCode : null;

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        process.WaitForExitAsync(cancellationToken);

    public void KillProcessTree()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    public ValueTask DisposeAsync()
    {
        process.Dispose();
        return ValueTask.CompletedTask;
    }
}
