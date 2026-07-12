using System.Text;
using System.Text.Json;
using TokenFish.Core.Models;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexAppServerSessionTests
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 7, 12, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartupStartsProcessExactlyOnce()
    {
        var factory = new FakeCodexAppServerProcessFactory(CreateProcess(InitializeResponse(1)));

        await using var session = await StartSessionAsync(factory);

        Assert.Equal(1, factory.StartCallCount);
    }

    [Fact]
    public async Task StartupBeginsDrainingStderr()
    {
        var process = CreateProcess(InitializeResponse(1));
        var factory = new FakeCodexAppServerProcessFactory(process);

        await using var session = await StartSessionAsync(factory);

        Assert.True(process.StandardErrorReader.ReadCallCount > 0);
    }

    [Fact]
    public async Task StartupInitializesProtocolExactlyOnce()
    {
        var process = CreateProcess(InitializeResponse(1));
        var factory = new FakeCodexAppServerProcessFactory(process);

        await using var session = await StartSessionAsync(factory);

        Assert.Single(process.StandardInputWriter.Lines, line => ReadMethod(line) == "initialize");
    }

    [Fact]
    public async Task StartupReturnsSessionOnlyAfterInitializationSucceeds()
    {
        var process = CreateProcess(InitializeResponse(1));
        var factory = new FakeCodexAppServerProcessFactory(process);

        await using var session = await StartSessionAsync(factory);

        Assert.Contains(process.StandardInputWriter.Lines, line => ReadMethod(line) == "initialized");
    }

    [Fact]
    public async Task StartupCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var expected = new OperationCanceledException(cancellation.Token);
        var factory = new FakeCodexAppServerProcessFactory(CreateProcess(InitializeResponse(1)))
        {
            StartException = expected
        };

        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StartSessionAsync(factory, cancellation.Token));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task InitializationFailureTerminatesAndDisposesProcess()
    {
        var process = CreateProcess("""{"id":1,"result":{"unexpected":}""");
        var factory = new FakeCodexAppServerProcessFactory(process);

        await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            StartSessionAsync(factory));

        Assert.Equal(1, process.KillProcessTreeCallCount);
        Assert.Equal(1, process.DisposeCallCount);
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task ProcessStartFailureProducesSanitizedException()
    {
        var factory = new FakeCodexAppServerProcessFactory(CreateProcess(InitializeResponse(1)))
        {
            StartException = new InvalidOperationException(@"C:\Users\person\codex.exe raw stderr")
        };

        var exception = await Assert.ThrowsAsync<CodexAppServerSessionException>(() =>
            StartSessionAsync(factory));

        Assert.Equal("Codex app server process could not be started.", exception.Message);
    }

    [Fact]
    public async Task ProcessStartFailureMessageDoesNotExposeExecutablePathOrStderr()
    {
        var factory = new FakeCodexAppServerProcessFactory(CreateProcess(InitializeResponse(1)))
        {
            StartException = new InvalidOperationException(@"C:\Users\person\codex.exe raw stderr")
        };

        var exception = await Assert.ThrowsAsync<CodexAppServerSessionException>(() =>
            StartSessionAsync(factory));

        Assert.DoesNotContain(@"C:\Users", exception.Message);
        Assert.DoesNotContain("raw stderr", exception.Message);
    }

    [Fact]
    public async Task CollectionUsesProtocolClientCollectorAndFactory()
    {
        var process = CreateProcess(
            InitializeResponse(1),
            RateLimitsResponse(2, primaryUsedPercent: 64),
            AccountUsageResponse(3, tokens: 321));
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));

        var snapshot = await session.CollectAsync(CancellationToken.None);

        Assert.Equal(
            ["initialize", "initialized", "account/rateLimits/read", "account/usage/read"],
            process.StandardInputWriter.Lines.Select(ReadMethod));
        Assert.Equal(ProviderKind.Codex, snapshot.Provider);
        Assert.Equal(64m, snapshot.UsageWindow.PercentageConsumed);
        Assert.Equal(321, snapshot.WeeklyTokens.TokenCount);
        Assert.Equal(CapturedAt, snapshot.CapturedAt);
    }

    [Fact]
    public async Task SequentialCollectionsAreSupported()
    {
        var process = CreateProcess(
            InitializeResponse(1),
            RateLimitsResponse(2, primaryUsedPercent: 20),
            AccountUsageResponse(3, tokens: 100),
            RateLimitsResponse(4, primaryUsedPercent: 30),
            AccountUsageResponse(5, tokens: 200));
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));

        var first = await session.CollectAsync(CancellationToken.None);
        var second = await session.CollectAsync(CancellationToken.None);

        Assert.Equal(20m, first.UsageWindow.PercentageConsumed);
        Assert.Equal(100, first.WeeklyTokens.TokenCount);
        Assert.Equal(30m, second.UsageWindow.PercentageConsumed);
        Assert.Equal(200, second.WeeklyTokens.TokenCount);
    }

    [Fact]
    public async Task CollectionAfterDisposalIsRejected()
    {
        var process = CreateProcess(InitializeResponse(1));
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));
        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            session.CollectAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CollectionCancellationPropagates()
    {
        var process = CreateProcess(InitializeResponse(1));
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.CollectAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task NoPartialResultIsFabricatedAfterProtocolFailure()
    {
        var process = CreateProcess(
            InitializeResponse(1),
            RateLimitsResponse(2));
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));
        ProviderUsageSnapshot? snapshot = null;

        await Assert.ThrowsAsync<CodexAppServerProtocolException>(async () =>
            snapshot = await session.CollectAsync(CancellationToken.None));

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task DisposalClosesStandardInput()
    {
        var process = CreateProcess(InitializeResponse(1));
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));

        await session.DisposeAsync();

        Assert.Equal(1, process.StandardInputWriter.DisposeCallCount);
    }

    [Fact]
    public async Task GracefullyExitedProcessIsNotKilled()
    {
        var process = CreateProcess(InitializeResponse(1));
        process.ExitOnStandardInputDispose = true;
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));

        await session.DisposeAsync();

        Assert.Equal(0, process.KillProcessTreeCallCount);
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task NonExitingProcessTreeIsTerminated()
    {
        var process = CreateProcess(InitializeResponse(1));
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process),
            shutdownTimeout: TimeSpan.Zero);

        await session.DisposeAsync();

        Assert.Equal(1, process.KillProcessTreeCallCount);
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task DisposalAwaitsProcessExit()
    {
        var process = CreateProcess(InitializeResponse(1));
        process.ExitOnStandardInputDispose = true;
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));

        await session.DisposeAsync();

        Assert.True(process.WaitForExitCallCount > 0);
    }

    [Fact]
    public async Task DisposalDisposesProcess()
    {
        var process = CreateProcess(InitializeResponse(1));
        process.ExitOnStandardInputDispose = true;
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));

        await session.DisposeAsync();

        Assert.Equal(1, process.DisposeCallCount);
    }

    [Fact]
    public async Task RepeatedDisposalIsSafe()
    {
        var process = CreateProcess(InitializeResponse(1));
        process.ExitOnStandardInputDispose = true;
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process));

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(1, process.StandardInputWriter.DisposeCallCount);
        Assert.Equal(1, process.DisposeCallCount);
    }

    [Fact]
    public async Task DisposalDoesNotLeaveOrphanProcessInLifecycle()
    {
        var process = CreateProcess(InitializeResponse(1));
        await using var session = await StartSessionAsync(
            new FakeCodexAppServerProcessFactory(process),
            shutdownTimeout: TimeSpan.Zero);

        await session.DisposeAsync();

        Assert.True(process.HasExited);
    }

    private static Task<CodexAppServerSession> StartSessionAsync(
        FakeCodexAppServerProcessFactory processFactory,
        CancellationToken cancellationToken = default,
        TimeSpan? shutdownTimeout = null) =>
        CodexAppServerSession.StartAsync(
            CodexAppServerLaunchCommand.CreateNative("codex.exe"),
            "1.0.0",
            new FakeTimeProvider(CapturedAt),
            processFactory,
            shutdownTimeout ?? TimeSpan.FromMilliseconds(50),
            cancellationToken);

    private static FakeCodexAppServerProcess CreateProcess(params string[] stdoutLines) =>
        new(stdoutLines);

    private static string InitializeResponse(long id) =>
        "{\"id\":" + id + ",\"result\":{}}";

    private static string RateLimitsResponse(long id, int primaryUsedPercent = 55) =>
        "{\"id\":" + id + ",\"result\":{\"rateLimits\":{\"primary\":{\"usedPercent\":" +
        primaryUsedPercent + ",\"windowDurationMins\":300,\"resetsAt\":1785000000}," +
        "\"secondary\":{\"usedPercent\":21,\"windowDurationMins\":10080,\"resetsAt\":1785604800}," +
        "\"rateLimitReachedType\":\"rate_limit_reached\"}}}";

    private static string AccountUsageResponse(long id, long tokens = 123) =>
        "{\"id\":" + id + ",\"result\":{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-12\"," +
        "\"tokens\":" + tokens + "}],\"summary\":{\"lifetimeTokens\":999,\"peakDailyTokens\":" +
        tokens + "}}}";

    private static string? ReadMethod(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("method").GetString();
    }

    private sealed class FakeCodexAppServerProcessFactory(
        FakeCodexAppServerProcess process) : ICodexAppServerProcessFactory
    {
        public Exception? StartException { get; init; }

        public int StartCallCount { get; private set; }

        public ICodexAppServerProcess Start(CodexAppServerLaunchCommand launchCommand)
        {
            StartCallCount++;

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

        public FakeCodexAppServerProcess(IReadOnlyList<string> stdoutLines)
        {
            StandardInputWriter = new RecordingTextWriter(() =>
            {
                if (ExitOnStandardInputDispose)
                {
                    MarkExited(0);
                }
            });
            StandardOutput = new StringReader(string.Join('\n', stdoutLines));
            StandardErrorReader = new CountingTextReader();
        }

        public RecordingTextWriter StandardInputWriter { get; }

        public CountingTextReader StandardErrorReader { get; }

        public bool ExitOnStandardInputDispose { get; set; }

        public int KillProcessTreeCallCount { get; private set; }

        public int WaitForExitCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public TextWriter StandardInput => StandardInputWriter;

        public TextReader StandardOutput { get; }

        public TextReader StandardError => StandardErrorReader;

        public bool HasExited => _hasExited;

        public int? ExitCode { get; private set; }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitForExitCallCount++;

            return _hasExited
                ? Task.CompletedTask
                : _exitCompletion.Task.WaitAsync(cancellationToken);
        }

        public void KillProcessTree()
        {
            KillProcessTreeCallCount++;
            MarkExited(-1);
        }

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

        public IReadOnlyList<string> Lines =>
            ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        public override Encoding Encoding => Encoding.UTF8;

        protected override void Dispose(bool disposing)
        {
            DisposeCallCount++;
            onDispose();
            base.Dispose(disposing);
        }
    }

    private sealed class CountingTextReader : TextReader
    {
        public int ReadCallCount { get; private set; }

        public override Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            ReadCallCount++;
            return Task.FromResult(0);
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
