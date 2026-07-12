using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexSessionUsageCollectorTests
{
    private static readonly DateTimeOffset CapturedAt =
        new(2026, 7, 12, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ProviderIsCodex()
    {
        var collector = CreateCollector();

        Assert.Equal(ProviderKind.Codex, collector.Provider);
    }

    [Fact]
    public void ConstructorDoesNotStartSession()
    {
        var factory = new FakeCodexAppServerSessionFactory();

        _ = CreateCollector(factory);

        Assert.Equal(0, factory.StartCallCount);
    }

    [Fact]
    public void ReadingProviderDoesNotStartSession()
    {
        var factory = new FakeCodexAppServerSessionFactory();
        var collector = CreateCollector(factory);

        _ = collector.Provider;

        Assert.Equal(0, factory.StartCallCount);
    }

    [Fact]
    public async Task FirstCollectionStartsExactlyOneSession()
    {
        var factory = new FakeCodexAppServerSessionFactory();
        var collector = CreateCollector(factory);

        await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(1, factory.StartCallCount);
    }

    [Fact]
    public async Task FirstCollectionDelegatesExactlyOnce()
    {
        var session = new FakeCodexAppServerSession();
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));

        await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(1, session.CollectCallCount);
    }

    [Fact]
    public async Task SequentialCollectionsReuseOneSession()
    {
        var session = new FakeCodexAppServerSession();
        var factory = new FakeCodexAppServerSessionFactory(session);
        var collector = CreateCollector(factory);

        await collector.CollectAsync(CancellationToken.None);
        await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(1, factory.StartCallCount);
        Assert.Equal(2, session.CollectCallCount);
    }

    [Fact]
    public async Task SequentialCollectionsDelegateOncePerCall()
    {
        var session = new FakeCodexAppServerSession();
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));

        await collector.CollectAsync(CancellationToken.None);
        await collector.CollectAsync(CancellationToken.None);
        await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(3, session.CollectCallCount);
    }

    [Fact]
    public async Task ConcurrentInitialCollectionsStartExactlyOneSession()
    {
        var factory = new FakeCodexAppServerSessionFactory();
        var collector = CreateCollector(factory);

        await Task.WhenAll(
            collector.CollectAsync(CancellationToken.None),
            collector.CollectAsync(CancellationToken.None),
            collector.CollectAsync(CancellationToken.None));

        Assert.Equal(1, factory.StartCallCount);
    }

    [Fact]
    public async Task ConcurrentCollectionOperationsDoNotOverlapAtSession()
    {
        var session = new FakeCodexAppServerSession
        {
            CollectionDelay = TimeSpan.FromMilliseconds(25)
        };
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));

        await Task.WhenAll(
            collector.CollectAsync(CancellationToken.None),
            collector.CollectAsync(CancellationToken.None),
            collector.CollectAsync(CancellationToken.None));

        Assert.Equal(0, session.OverlappingCollectCallCount);
    }

    [Fact]
    public async Task LaunchCommandIsPassedToFactoryUnchanged()
    {
        var factory = new FakeCodexAppServerSessionFactory();
        var launchCommand = CodexAppServerLaunchCommand.CreateWsl("Ubuntu");
        var collector = CreateCollector(factory, launchCommand);

        await collector.CollectAsync(CancellationToken.None);

        Assert.Same(launchCommand, factory.LaunchCommand);
    }

    [Fact]
    public async Task ClientVersionIsPassedToFactoryUnchanged()
    {
        var factory = new FakeCodexAppServerSessionFactory();
        var collector = CreateCollector(factory, clientVersion: "2.3.4");

        await collector.CollectAsync(CancellationToken.None);

        Assert.Equal("2.3.4", factory.ClientVersion);
    }

    [Fact]
    public async Task StartupCancellationTokenIsPassedThrough()
    {
        var factory = new FakeCodexAppServerSessionFactory();
        var collector = CreateCollector(factory);
        using var cancellation = new CancellationTokenSource();

        await collector.CollectAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, factory.CancellationToken);
    }

    [Fact]
    public async Task CollectionCancellationTokenIsPassedThrough()
    {
        var session = new FakeCodexAppServerSession();
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));
        using var cancellation = new CancellationTokenSource();

        await collector.CollectAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, session.CancellationToken);
    }

    [Fact]
    public async Task StartupFailurePropagates()
    {
        var expected = new CodexAppServerSessionException("sanitized failure");
        var factory = new FakeCodexAppServerSessionFactory
        {
            StartException = expected
        };
        var collector = CreateCollector(factory);

        var actual = await Assert.ThrowsAsync<CodexAppServerSessionException>(() =>
            collector.CollectAsync(CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task FailedStartupIsNotCached()
    {
        var factory = new FakeCodexAppServerSessionFactory
        {
            StartException = new CodexAppServerSessionException("sanitized failure")
        };
        var collector = CreateCollector(factory);

        await Assert.ThrowsAsync<CodexAppServerSessionException>(() =>
            collector.CollectAsync(CancellationToken.None));
        factory.StartException = null;
        await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(2, factory.StartCallCount);
    }

    [Fact]
    public async Task LaterCallCanAttemptStartupAgainAfterStartupFailure()
    {
        var session = new FakeCodexAppServerSession();
        var factory = new FakeCodexAppServerSessionFactory(session)
        {
            StartException = new CodexAppServerSessionException("sanitized failure")
        };
        var collector = CreateCollector(factory);

        await Assert.ThrowsAsync<CodexAppServerSessionException>(() =>
            collector.CollectAsync(CancellationToken.None));
        factory.StartException = null;

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(2, factory.StartCallCount);
        Assert.Equal(ProviderKind.Codex, snapshot.Provider);
    }

    [Fact]
    public async Task CollectionFailurePropagates()
    {
        var expected = new InvalidOperationException("collection failure");
        var session = new FakeCodexAppServerSession
        {
            CollectException = expected
        };
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            collector.CollectAsync(CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task NoPartialSnapshotIsReturnedAfterFailure()
    {
        var session = new FakeCodexAppServerSession
        {
            CollectException = new InvalidOperationException("collection failure")
        };
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));
        ProviderUsageSnapshot? snapshot = null;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            snapshot = await collector.CollectAsync(CancellationToken.None));

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task DisposalBeforeCollectionDoesNotStartSession()
    {
        var factory = new FakeCodexAppServerSessionFactory();
        var collector = CreateCollector(factory);

        await collector.DisposeAsync();

        Assert.Equal(0, factory.StartCallCount);
    }

    [Fact]
    public async Task DisposalDisposesExistingSessionExactlyOnce()
    {
        var session = new FakeCodexAppServerSession();
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));
        await collector.CollectAsync(CancellationToken.None);

        await collector.DisposeAsync();

        Assert.Equal(1, session.DisposeCallCount);
    }

    [Fact]
    public async Task RepeatedDisposalIsSafe()
    {
        var session = new FakeCodexAppServerSession();
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));
        await collector.CollectAsync(CancellationToken.None);

        await collector.DisposeAsync();
        await collector.DisposeAsync();

        Assert.Equal(1, session.DisposeCallCount);
    }

    [Fact]
    public async Task CollectionAfterDisposalIsRejected()
    {
        var collector = CreateCollector();
        await collector.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            collector.CollectAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DisposalWaitsForActiveCollection()
    {
        var session = new FakeCodexAppServerSession
        {
            BlockCollection = true
        };
        var collector = CreateCollector(new FakeCodexAppServerSessionFactory(session));

        var collectionTask = collector.CollectAsync(CancellationToken.None);
        await session.CollectionStarted.Task;
        var disposeTask = collector.DisposeAsync().AsTask();

        var earlyCompletion = await Task.WhenAny(disposeTask, Task.Delay(50));
        Assert.NotSame(disposeTask, earlyCompletion);
        Assert.Equal(0, session.DisposeCallCount);

        session.ReleaseCollection();
        await collectionTask;
        await disposeTask;

        Assert.Equal(1, session.DisposeCallCount);
    }

    [Fact]
    public void NullFactoryIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CodexSessionUsageCollector(
                null!,
                CodexAppServerLaunchCommand.CreateNative("codex.exe"),
                "1.0.0"));
    }

    [Fact]
    public void NullLaunchCommandIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CodexSessionUsageCollector(
                new FakeCodexAppServerSessionFactory(),
                null!,
                "1.0.0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidClientVersionIsRejected(string? clientVersion)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new CodexSessionUsageCollector(
                new FakeCodexAppServerSessionFactory(),
                CodexAppServerLaunchCommand.CreateNative("codex.exe"),
                clientVersion!));
    }

    [Fact]
    public async Task CoordinatorClaudeOnlyModeDoesNotStartCodexSessionFactory()
    {
        var factory = new FakeCodexAppServerSessionFactory();
        var coordinator = new ProviderUsageCollectionCoordinator(
        [
            new SyntheticProviderUsageCollector(ProviderKind.Claude),
            CreateCollector(factory)
        ]);

        var snapshots = await coordinator.CollectAsync(
            ProviderSelectionMode.ClaudeOnly,
            CancellationToken.None);

        Assert.Equal(0, factory.StartCallCount);
        Assert.Equal(ProviderKind.Claude, Assert.Single(snapshots).Provider);
    }

    private static CodexSessionUsageCollector CreateCollector(
        FakeCodexAppServerSessionFactory? sessionFactory = null,
        CodexAppServerLaunchCommand? launchCommand = null,
        string clientVersion = "1.0.0") =>
        new(
            sessionFactory ?? new FakeCodexAppServerSessionFactory(),
            launchCommand ?? CodexAppServerLaunchCommand.CreateNative("codex.exe"),
            clientVersion);

    private static ProviderUsageSnapshot CreateSnapshot(ProviderKind provider = ProviderKind.Codex) =>
        new(
            provider,
            ProviderConnectionState.Connected,
            PercentageUsageMetric.Unavailable(DataAuthority.LocalProviderReported, DataFreshness.Live),
            null,
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            CapturedAt);

    private sealed class FakeCodexAppServerSessionFactory(
        FakeCodexAppServerSession? session = null) : ICodexAppServerSessionFactory
    {
        private readonly FakeCodexAppServerSession _session = session ?? new();

        public Exception? StartException { get; set; }

        public int StartCallCount { get; private set; }

        public CodexAppServerLaunchCommand? LaunchCommand { get; private set; }

        public string? ClientVersion { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<ICodexAppServerSession> StartAsync(
            CodexAppServerLaunchCommand launchCommand,
            string clientVersion,
            CancellationToken cancellationToken)
        {
            StartCallCount++;
            LaunchCommand = launchCommand;
            ClientVersion = clientVersion;
            CancellationToken = cancellationToken;

            if (StartException is not null)
            {
                return Task.FromException<ICodexAppServerSession>(StartException);
            }

            return Task.FromResult<ICodexAppServerSession>(_session);
        }
    }

    private sealed class FakeCodexAppServerSession : ICodexAppServerSession
    {
        private int _activeCollectionCount;
        private readonly TaskCompletionSource _collectionRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? CollectException { get; init; }

        public TimeSpan CollectionDelay { get; init; }

        public bool BlockCollection { get; init; }

        public int CollectCallCount { get; private set; }

        public int OverlappingCollectCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public TaskCompletionSource CollectionStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            CollectCallCount++;
            CancellationToken = cancellationToken;
            if (Interlocked.Increment(ref _activeCollectionCount) > 1)
            {
                OverlappingCollectCallCount++;
            }

            CollectionStarted.TrySetResult();

            try
            {
                if (CollectException is not null)
                {
                    throw CollectException;
                }

                if (BlockCollection)
                {
                    await _collectionRelease.Task.ConfigureAwait(false);
                }

                if (CollectionDelay > TimeSpan.Zero)
                {
                    await Task.Delay(CollectionDelay, cancellationToken).ConfigureAwait(false);
                }

                return CreateSnapshot();
            }
            finally
            {
                Interlocked.Decrement(ref _activeCollectionCount);
            }
        }

        public void ReleaseCollection() => _collectionRelease.TrySetResult();

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SyntheticProviderUsageCollector(ProviderKind provider) : IProviderUsageCollector
    {
        public ProviderKind Provider => provider;

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CreateSnapshot(provider));
    }
}
