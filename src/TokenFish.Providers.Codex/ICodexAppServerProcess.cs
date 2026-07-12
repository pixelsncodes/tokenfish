namespace TokenFish.Providers.Codex;

internal interface ICodexAppServerProcess : IAsyncDisposable
{
    TextWriter StandardInput { get; }

    TextReader StandardOutput { get; }

    TextReader StandardError { get; }

    bool HasExited { get; }

    int? ExitCode { get; }

    Task WaitForExitAsync(CancellationToken cancellationToken);

    void KillProcessTree();
}
