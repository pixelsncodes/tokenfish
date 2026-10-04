using TokenFish.Core.Models;

namespace TokenFish.Providers.Codex;

public sealed class CodexAppServerSession : ICodexAppServerSession
{
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(2);

    private readonly ICodexAppServerProcess _process;
    private readonly Task _stderrDrainTask;
    private readonly CodexProviderUsageCollector _collector;
    private readonly TimeSpan _shutdownTimeout;
    private readonly TimeSpan _collectionTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _initialized;
    private bool _disposeStarted;
    private bool _disposed;

    public bool IsHealthy => !_disposed && !_process.HasExited && !_collector.RequiresSessionRecovery;

    private CodexAppServerSession(
        ICodexAppServerProcess process,
        Task stderrDrainTask,
        CodexProviderUsageCollector collector,
        TimeSpan shutdownTimeout,
        TimeSpan collectionTimeout)
    {
        _process = process;
        _stderrDrainTask = stderrDrainTask;
        _collector = collector;
        _shutdownTimeout = shutdownTimeout;
        _collectionTimeout = collectionTimeout;
        _initialized = true;
    }

    public static Task<CodexAppServerSession> StartAsync(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion,
        TimeProvider? timeProvider,
        CancellationToken cancellationToken) =>
        StartAsync(
            launchCommand,
            clientVersion,
            timeProvider,
            new CodexAppServerProcessFactory(),
            DefaultShutdownTimeout,
            cancellationToken);

    internal static Task<CodexAppServerSession> StartAsync(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion,
        TimeProvider? timeProvider,
        ICodexAppServerProcessFactory processFactory,
        CancellationToken cancellationToken) =>
        StartAsync(
            launchCommand,
            clientVersion,
            timeProvider,
            processFactory,
            DefaultShutdownTimeout,
            cancellationToken);

    internal static async Task<CodexAppServerSession> StartAsync(
        CodexAppServerLaunchCommand launchCommand,
        string clientVersion,
        TimeProvider? timeProvider,
        ICodexAppServerProcessFactory processFactory,
        TimeSpan shutdownTimeout,
        CancellationToken cancellationToken,
        TimeSpan? startupTimeout = null,
        TimeSpan? collectionTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
        ArgumentNullException.ThrowIfNull(processFactory);

        ICodexAppServerProcess process;
        try
        {
            process = processFactory.Start(launchCommand);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            throw new CodexAppServerSessionException(
                "Codex app server process could not be started.");
        }

        var stderrDrainTask = DrainStandardErrorAsync(process.StandardError);

        try
        {
            var protocolClient = new CodexAppServerProtocolClient(
                process.StandardOutput,
                process.StandardInput,
                clientVersion);

            using var startupDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startupDeadline.CancelAfter(startupTimeout ?? TimeSpan.FromSeconds(15));
            try
            {
                await protocolClient.InitializeAsync(startupDeadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new CodexAppServerSessionException("Codex startup timed out. Check the runtime and try refreshing again.");
            }

            var snapshotFactory = new CodexUsageSnapshotFactory();
            var collector = new CodexProviderUsageCollector(
                protocolClient,
                snapshotFactory,
                timeProvider);

            return new CodexAppServerSession(
                process,
                stderrDrainTask,
                collector,
                shutdownTimeout,
                collectionTimeout ?? TimeSpan.FromSeconds(20));
        }
        catch
        {
            await DisposeAfterFailedStartupAsync(process, stderrDrainTask).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        if (_disposeStarted)
        {
            throw new ObjectDisposedException(nameof(CodexAppServerSession));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "Codex app server session is not initialized.");
            }

            if (_disposeStarted)
            {
                throw new ObjectDisposedException(nameof(CodexAppServerSession));
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_collectionTimeout);
            try
            {
                return await _collector.CollectAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new CodexAppServerSessionException("Codex refresh timed out. Try refreshing again.");
            }
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

            await ShutdownProcessAsync().ConfigureAwait(false);
            _initialized = false;
            _disposed = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ShutdownProcessAsync()
    {
        TryCloseStandardInput(_process);

        if (!_process.HasExited)
        {
            using var shutdown = new CancellationTokenSource(_shutdownTimeout);
            try
            {
                await _process.WaitForExitAsync(shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!_process.HasExited)
                {
                    TryKillProcessTree(_process);
                }
            }
        }

        await WaitForProcessExitSafelyAsync(_process).ConfigureAwait(false);
        await AwaitStderrDrainSafelyAsync(_stderrDrainTask).ConfigureAwait(false);
        await _process.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task DisposeAfterFailedStartupAsync(
        ICodexAppServerProcess process,
        Task stderrDrainTask)
    {
        if (!process.HasExited)
        {
            TryKillProcessTree(process);
        }

        await WaitForProcessExitSafelyAsync(process).ConfigureAwait(false);
        await AwaitStderrDrainSafelyAsync(stderrDrainTask).ConfigureAwait(false);
        await process.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task DrainStandardErrorAsync(TextReader stderr)
    {
        var buffer = new char[4096];

        try
        {
            while (await stderr.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is ObjectDisposedException ||
            exception is InvalidOperationException)
        {
        }
    }

    private static void TryCloseStandardInput(ICodexAppServerProcess process)
    {
        try
        {
            process.StandardInput.Dispose();
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is ObjectDisposedException ||
            exception is InvalidOperationException)
        {
        }
    }

    private static void TryKillProcessTree(ICodexAppServerProcess process)
    {
        try
        {
            process.KillProcessTree();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is NotSupportedException)
        {
        }
    }

    private static async Task WaitForProcessExitSafelyAsync(ICodexAppServerProcess process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is ObjectDisposedException)
        {
        }
    }

    private static async Task AwaitStderrDrainSafelyAsync(Task stderrDrainTask)
    {
        try
        {
            await stderrDrainTask.ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is ObjectDisposedException ||
            exception is InvalidOperationException)
        {
        }
    }
}
