using System.Text;
using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexRuntimeServicesTests
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 7, 12, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreatingServicesDoesNotStartCodexProcessOrSession()
    {
        var processFactory = new FakeCodexAppServerProcessFactory(CreateProcess());

        await using var services = CreateServices(processFactory);

        Assert.Equal(0, processFactory.StartCallCount);
    }

    [Fact]
    public async Task ResolvingCollectorDoesNotStartCodexProcessOrSession()
    {
        var processFactory = new FakeCodexAppServerProcessFactory(CreateProcess());
        await using var services = CreateServices(processFactory);

        _ = services.CodexUsageCollector;

        Assert.Equal(0, processFactory.StartCallCount);
    }

    [Fact]
    public async Task ResolvedCollectorReportsCodexProviderWithoutStartingCodex()
    {
        var processFactory = new FakeCodexAppServerProcessFactory(CreateProcess());
        await using var services = CreateServices(processFactory);

        var collector = services.CodexUsageCollector;

        Assert.Equal(ProviderKind.Codex, collector.Provider);
        Assert.Equal(0, processFactory.StartCallCount);
    }

    [Fact]
    public async Task CollectorIsReturnedThroughSharedProviderContract()
    {
        await using var services = CreateServices(
            new FakeCodexAppServerProcessFactory(CreateProcess()));

        Assert.IsAssignableFrom<IProviderUsageCollector>(services.CodexUsageCollector);
    }

    [Fact]
    public async Task CollectorIsSafeApplicationFacingCollector()
    {
        await using var services = CreateServices(
            new FakeCodexAppServerProcessFactory(CreateProcess()));

        Assert.IsType<CodexRuntimeFailureMappingCollector>(services.CodexUsageCollector);
    }

    [Fact]
    public async Task ExpectedStartupFailureReturnsSafeSnapshot()
    {
        var processFactory = new FakeCodexAppServerProcessFactory(CreateProcess())
        {
            StartException = new InvalidOperationException("sanitized startup failure")
        };
        await using var services = CreateServices(processFactory);

        var snapshot = await services.CodexUsageCollector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderKind.Codex, snapshot.Provider);
        Assert.Equal(ProviderConnectionState.Disconnected, snapshot.ConnectionState);
        Assert.False(snapshot.UsageWindow.IsAvailable);
        Assert.False(snapshot.WeeklyTokens.IsAvailable);
        Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.Equal(CapturedAt, snapshot.CapturedAt);
    }

    [Fact]
    public async Task ExplicitLaunchCommandReachesSessionAndProcessConstructionPath()
    {
        var launchCommand = CodexAppServerLaunchCommand.CreateWsl("Ubuntu-24.04");
        var processFactory = new FakeCodexAppServerProcessFactory(CreateProcess());
        await using var services = CreateServices(processFactory, launchCommand);

        var snapshot = await services.CodexUsageCollector.CollectAsync(CancellationToken.None);

        Assert.Equal(1, processFactory.StartCallCount);
        Assert.Same(launchCommand, processFactory.LaunchCommand);
        Assert.Equal(ProviderKind.Codex, snapshot.Provider);
    }

    [Fact]
    public async Task DisposingBeforeCollectionDoesNotStartCodex()
    {
        var processFactory = new FakeCodexAppServerProcessFactory(CreateProcess());
        await using var services = CreateServices(processFactory);

        await services.DisposeAsync();

        Assert.Equal(0, processFactory.StartCallCount);
    }

    [Fact]
    public async Task CollectionAfterDisposalIsRejected()
    {
        await using var services = CreateServices(
            new FakeCodexAppServerProcessFactory(CreateProcess()));
        var collector = services.CodexUsageCollector;
        await services.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            collector.CollectAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DisposingInitializedRuntimeIsForwardedExactlyOnce()
    {
        var process = CreateProcess();
        var processFactory = new FakeCodexAppServerProcessFactory(process);
        await using var services = CreateServices(processFactory);

        await services.CodexUsageCollector.CollectAsync(CancellationToken.None);
        await services.DisposeAsync();
        await services.DisposeAsync();

        Assert.Equal(1, processFactory.StartCallCount);
        Assert.Equal(1, process.StandardInputWriter.DisposeCallCount);
        Assert.Equal(1, process.DisposeCallCount);
    }

    private static CodexRuntimeServices CreateServices(
        FakeCodexAppServerProcessFactory processFactory,
        CodexAppServerLaunchCommand? launchCommand = null) =>
        CodexRuntimeServices.Create(
            launchCommand ?? CodexAppServerLaunchCommand.CreateNative("codex.exe"),
            "1.0.0",
            processFactory,
            new FakeTimeProvider(CapturedAt));

    private static FakeCodexAppServerProcess CreateProcess() =>
        new(
            InitializeResponse(1),
            RateLimitsResponse(2),
            AccountUsageResponse(3));

    private static string InitializeResponse(long id) =>
        "{\"id\":" + id + ",\"result\":{}}";

    private static string RateLimitsResponse(long id) =>
        "{\"id\":" + id + ",\"result\":{\"rateLimits\":{\"primary\":{\"usedPercent\":64," +
        "\"windowDurationMins\":300,\"resetsAt\":1785000000},\"secondary\":{\"usedPercent\":21," +
        "\"windowDurationMins\":10080,\"resetsAt\":1785604800}," +
        "\"rateLimitReachedType\":\"rate_limit_reached\"}}}";

    private static string AccountUsageResponse(long id) =>
        "{\"id\":" + id + ",\"result\":{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-12\"," +
        "\"tokens\":321}],\"summary\":{\"lifetimeTokens\":999,\"peakDailyTokens\":321}}}";

    private sealed class FakeCodexAppServerProcessFactory(
        FakeCodexAppServerProcess process) : ICodexAppServerProcessFactory
    {
        public Exception? StartException { get; init; }

        public int StartCallCount { get; private set; }

        public CodexAppServerLaunchCommand? LaunchCommand { get; private set; }

        public ICodexAppServerProcess Start(CodexAppServerLaunchCommand launchCommand)
        {
            StartCallCount++;
            LaunchCommand = launchCommand;

            if (StartException is not null)
            {
                throw StartException;
            }

            return process;
        }
    }

    private sealed class FakeCodexAppServerProcess : ICodexAppServerProcess
    {
        private readonly TaskCompletionSource _exitCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _hasExited;

        public FakeCodexAppServerProcess(params string[] stdoutLines)
        {
            StandardInputWriter = new RecordingTextWriter(() => MarkExited(0));
            StandardOutput = new StringReader(string.Join('\n', stdoutLines));
            StandardError = new StringReader(string.Empty);
        }

        public RecordingTextWriter StandardInputWriter { get; }

        public int DisposeCallCount { get; private set; }

        public TextWriter StandardInput => StandardInputWriter;

        public TextReader StandardOutput { get; }

        public TextReader StandardError { get; }

        public bool HasExited => _hasExited;

        public int? ExitCode { get; private set; }

        public Task WaitForExitAsync(CancellationToken cancellationToken) =>
            _hasExited
                ? Task.CompletedTask
                : _exitCompletion.Task.WaitAsync(cancellationToken);

        public void KillProcessTree() => MarkExited(-1);

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }

        private void MarkExited(int exitCode)
        {
            if (_hasExited)
            {
                return;
            }

            ExitCode = exitCode;
            _hasExited = true;
            _exitCompletion.TrySetResult();
        }
    }

    private sealed class RecordingTextWriter(Action onDispose) : StringWriter
    {
        public int DisposeCallCount { get; private set; }

        public override Encoding Encoding => Encoding.UTF8;

        protected override void Dispose(bool disposing)
        {
            DisposeCallCount++;
            onDispose();
            base.Dispose(disposing);
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
