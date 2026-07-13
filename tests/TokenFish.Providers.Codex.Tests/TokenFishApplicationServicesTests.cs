using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.Providers.Codex.Tests;

public sealed class TokenFishApplicationServicesTests
{
    [Fact]
    public async Task CreatingApplicationServicesFromSettingsIsLazy()
    {
        var factory = new RecordingCodexRuntimeFactory();

        await using var services = CreateServices(factory);

        Assert.Equal(1, factory.CreateCallCount);
        Assert.Equal(0, factory.Collector.CollectCallCount);
    }

    [Fact]
    public async Task ResolvingCollectorDoesNotStartCodexOrWsl()
    {
        var factory = new RecordingCodexRuntimeFactory();
        await using var services = CreateServices(factory);

        _ = services.CodexUsageCollector;

        Assert.Equal(0, factory.Collector.CollectCallCount);
    }

    [Fact]
    public async Task ResolvingRefreshLifecycleDoesNotCollectOrStartCodexOrWsl()
    {
        var factory = new RecordingCodexRuntimeFactory();
        await using var services = CreateServices(factory);

        _ = services.ProviderRefreshLifecycle;

        Assert.Equal(1, factory.CreateCallCount);
        Assert.Equal(0, factory.Collector.CollectCallCount);
    }

    [Fact]
    public async Task DefaultCompositionUsesCodexOnlyAndDoesNotRequireClaudeRuntime()
    {
        var factory = new RecordingCodexRuntimeFactory();

        await using var services = CreateServices(factory, new AppSettings());

        Assert.Same(factory.Collector, services.CodexUsageCollector);
        Assert.Equal(1, factory.CreateCallCount);
        Assert.Equal(0, factory.Collector.CollectCallCount);
    }

    [Fact]
    public async Task ApplicationCompositionUsesSettingsSelectedLaunchCommand()
    {
        var factory = new RecordingCodexRuntimeFactory();
        var settings = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.CodexOnly,
            CodexRuntimeMode = CodexRuntimeMode.WslLoginShell,
            CodexWslDistributionName = "Ubuntu-24.04"
        };

        await using var services = CreateServices(factory, settings);

        Assert.Equal(
            [
                "--distribution",
                "Ubuntu-24.04",
                "--exec",
                "bash",
                "-lc",
                "exec codex app-server --stdio"
            ],
            factory.LaunchCommand!.Arguments);
        Assert.Same(factory.Collector, services.CodexUsageCollector);
    }

    [Fact]
    public async Task ClaudeOnlyCompositionDoesNotConstructCodexRuntime()
    {
        var factory = new RecordingCodexRuntimeFactory();
        var claudeFactory = new RecordingClaudeCollectorFactory();
        var settings = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly,
            CodexRuntimeMode = (CodexRuntimeMode)999
        };

        await using var services = CreateServices(factory, settings, claudeFactory.Create);

        Assert.Null(services.CodexUsageCollector);
        Assert.Same(claudeFactory.Collector, services.ClaudeUsageCollector);
        Assert.Equal(0, factory.CreateCallCount);
        Assert.Equal(1, claudeFactory.CreateCallCount);
    }

    [Fact]
    public async Task ClaudeOnlyRefreshLifecycleDoesNotConstructCodexRuntime()
    {
        var factory = new RecordingCodexRuntimeFactory();
        var claudeFactory = new RecordingClaudeCollectorFactory();
        var settings = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly
        };

        await using var services = CreateServices(factory, settings, claudeFactory.Create);

        _ = services.ProviderRefreshLifecycle;

        Assert.Null(services.CodexUsageCollector);
        Assert.Same(claudeFactory.Collector, services.ClaudeUsageCollector);
        Assert.Equal(0, factory.CreateCallCount);
        Assert.Equal(1, claudeFactory.CreateCallCount);
    }

    [Fact]
    public async Task CodexOnlyCompositionDoesNotConstructClaudeCollector()
    {
        var factory = new RecordingCodexRuntimeFactory();
        var claudeFactory = new RecordingClaudeCollectorFactory();
        var settings = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.CodexOnly
        };

        await using var services = CreateServices(factory, settings, claudeFactory.Create);

        Assert.Null(services.ClaudeUsageCollector);
        Assert.Same(factory.Collector, services.CodexUsageCollector);
        Assert.Equal(0, claudeFactory.CreateCallCount);
        Assert.Equal(1, factory.CreateCallCount);
    }

    [Fact]
    public async Task BothProviderCompositionConstructsBothProviders()
    {
        var factory = new RecordingCodexRuntimeFactory();
        var claudeFactory = new RecordingClaudeCollectorFactory();
        var settings = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.Both
        };

        await using var services = CreateServices(factory, settings, claudeFactory.Create);

        Assert.Same(claudeFactory.Collector, services.ClaudeUsageCollector);
        Assert.Same(factory.Collector, services.CodexUsageCollector);
        Assert.Equal(1, claudeFactory.CreateCallCount);
        Assert.Equal(1, factory.CreateCallCount);
    }

    [Fact]
    public async Task BothProviderRefreshInvokesBothProviders()
    {
        var factory = new RecordingCodexRuntimeFactory(throwOnCollect: false);
        var claudeFactory = new RecordingClaudeCollectorFactory();
        var settings = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.Both
        };
        await using var services = CreateServices(factory, settings, claudeFactory.Create);

        var snapshots = await services.ProviderRefreshLifecycle.RefreshAsync(CancellationToken.None);

        Assert.Equal([ProviderKind.Claude, ProviderKind.Codex], snapshots.Select(snapshot => snapshot.Provider));
        Assert.Equal(1, claudeFactory.Collector.CollectCallCount);
        Assert.Equal(1, factory.Collector.CollectCallCount);
    }

    [Fact]
    public void InvalidRuntimeModeFailsBeforeCodexRuntimeFactoryIsInvoked()
    {
        var factory = new RecordingCodexRuntimeFactory();
        var settings = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.CodexOnly,
            CodexRuntimeMode = (CodexRuntimeMode)999
        };

        var exception = Assert.Throws<CodexRuntimeConfigurationException>(() =>
            CreateServices(factory, settings));

        Assert.Equal("Codex runtime mode is not supported.", exception.Message);
        Assert.Equal(0, factory.CreateCallCount);
    }

    [Fact]
    public async Task DisposalForwardsToCodexRuntimeOwnerExactlyOnce()
    {
        var factory = new RecordingCodexRuntimeFactory();
        var services = CreateServices(factory);

        await services.DisposeAsync();
        await services.DisposeAsync();

        Assert.Equal(1, factory.Owner.DisposeCallCount);
    }

    [Fact]
    public async Task DisposalWithoutCodexRuntimeIsSafe()
    {
        var services = TokenFishApplicationServices.Create(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly },
            "1.0.0",
            TimeSpan.FromMinutes(1),
            new ManualTimeProvider(),
            (_, _) => throw new InvalidOperationException("should not construct Codex"),
            (_, _) => new RecordingProviderUsageCollector(ProviderKind.Claude));

        await services.DisposeAsync();
        await services.DisposeAsync();

        Assert.Null(services.CodexUsageCollector);
    }

    private static TokenFishApplicationServices CreateServices(
        RecordingCodexRuntimeFactory factory,
        AppSettings? settings = null,
        Func<TimeProvider, TimeSpan, IProviderUsageCollector>? createClaudeCollector = null) =>
        TokenFishApplicationServices.Create(
            settings ?? new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            "1.0.0",
            TimeSpan.FromMinutes(1),
            new ManualTimeProvider(),
            factory.Create,
            createClaudeCollector);

    private sealed class RecordingCodexRuntimeFactory
    {
        public RecordingCodexRuntimeFactory(bool throwOnCollect = true)
        {
            Collector = new RecordingProviderUsageCollector(
                ProviderKind.Codex,
                throwOnCollect
                    ? _ => throw new InvalidOperationException("collection is not expected in composition tests.")
                    : null);
        }

        public RecordingProviderUsageCollector Collector { get; }

        public RecordingAsyncDisposable Owner { get; } = new();

        public int CreateCallCount { get; private set; }

        public CodexAppServerLaunchCommand? LaunchCommand { get; private set; }

        public string? ClientVersion { get; private set; }

        public TokenFishApplicationServices.ProviderRuntime Create(
            CodexAppServerLaunchCommand launchCommand,
            string clientVersion)
        {
            CreateCallCount++;
            LaunchCommand = launchCommand;
            ClientVersion = clientVersion;

            return new TokenFishApplicationServices.ProviderRuntime(Collector, Owner);
        }
    }

    private sealed class RecordingProviderUsageCollector : IProviderUsageCollector
    {
        private readonly Func<CancellationToken, Task<ProviderUsageSnapshot>> _collectAsync;

        public RecordingProviderUsageCollector(
            ProviderKind provider,
            Func<CancellationToken, Task<ProviderUsageSnapshot>>? collectAsync = null)
        {
            Provider = provider;
            _collectAsync = collectAsync ?? (_ => Task.FromResult(CreateSnapshot(provider)));
        }

        public int CollectCallCount { get; private set; }

        public ProviderKind Provider { get; }

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            CollectCallCount++;
            return _collectAsync(cancellationToken);
        }

        private static ProviderUsageSnapshot CreateSnapshot(ProviderKind provider) =>
            new(
                provider,
                ProviderConnectionState.Connected,
                PercentageUsageMetric.Unavailable(
                    DataAuthority.LocalProviderReported,
                    DataFreshness.Unknown),
                null,
                TokenCountMetric.Unavailable(
                    DataAuthority.TokenFishDerived,
                    DataFreshness.Unknown),
                TokenCountMetric.Unavailable(
                    DataAuthority.TokenFishDerived,
                    DataFreshness.Unknown),
                DateTimeOffset.UnixEpoch);
    }

    private sealed class RecordingClaudeCollectorFactory
    {
        public RecordingProviderUsageCollector Collector { get; } =
            new(ProviderKind.Claude);

        public int CreateCallCount { get; private set; }

        public IProviderUsageCollector Create(
            TimeProvider timeProvider,
            TimeSpan freshnessThreshold)
        {
            _ = timeProvider;
            _ = freshnessThreshold;
            CreateCallCount++;
            return Collector;
        }
    }

    private sealed class RecordingAsyncDisposable : IAsyncDisposable
    {
        public int DisposeCallCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }
}
