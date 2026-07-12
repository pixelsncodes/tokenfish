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
        var settings = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly,
            CodexRuntimeMode = (CodexRuntimeMode)999
        };

        await using var services = CreateServices(factory, settings);

        Assert.Null(services.CodexUsageCollector);
        Assert.Equal(0, factory.CreateCallCount);
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
            (_, _) => throw new InvalidOperationException("should not construct Codex"));

        await services.DisposeAsync();
        await services.DisposeAsync();

        Assert.Null(services.CodexUsageCollector);
    }

    private static TokenFishApplicationServices CreateServices(
        RecordingCodexRuntimeFactory factory,
        AppSettings? settings = null) =>
        TokenFishApplicationServices.Create(
            settings ?? new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            "1.0.0",
            factory.Create);

    private sealed class RecordingCodexRuntimeFactory
    {
        public RecordingProviderUsageCollector Collector { get; } = new();

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
        public int CollectCallCount { get; private set; }

        public ProviderKind Provider => ProviderKind.Codex;

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            CollectCallCount++;
            throw new InvalidOperationException("collection is not expected in composition tests.");
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
}
