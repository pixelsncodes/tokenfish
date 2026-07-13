using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class ProviderReadinessProviderTests
{
    [Fact]
    public void CodexSelectedWithValidConfigurationAndNoSnapshotWaitsForData()
    {
        var provider = new ProviderReadinessProvider(snapshotStore: null);

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.CodexOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderKind.Codex, row.Provider);
        Assert.Equal(ProviderReadinessKind.WaitingForData, row.Kind);
        Assert.Equal("Waiting for data", row.Label);
    }

    [Fact]
    public void CodexSelectedWithAvailableSnapshotIsReady()
    {
        var provider = new ProviderReadinessProvider(CreateStore(
            CreateSnapshot(ProviderKind.Codex, ProviderConnectionState.Connected, DataFreshness.Live)));

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.CodexOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderReadinessKind.Ready, row.Kind);
        Assert.Equal("Ready", row.Label);
    }

    [Fact]
    public void CodexSelectedWithUnavailableProviderStateIsUnavailable()
    {
        var provider = new ProviderReadinessProvider(CreateStore(
            CreateSnapshot(ProviderKind.Codex, ProviderConnectionState.Disconnected, DataFreshness.Unknown)));

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.CodexOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderReadinessKind.Unavailable, row.Kind);
        Assert.Equal("Unavailable", row.Label);
    }

    [Fact]
    public void ClaudeSelectedWithNoBridgeSnapshotWaitsForData()
    {
        var provider = new ProviderReadinessProvider(snapshotStore: null);

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.ClaudeOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderKind.Claude, row.Provider);
        Assert.Equal(ProviderReadinessKind.WaitingForData, row.Kind);
        Assert.Equal("Waiting for Claude Code to send usage through the local bridge.", row.Description);
    }

    [Fact]
    public void ClaudeSelectedWithFreshBridgeSnapshotIsReady()
    {
        var provider = new ProviderReadinessProvider(CreateStore(
            CreateSnapshot(ProviderKind.Claude, ProviderConnectionState.Connected, DataFreshness.Live)));

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.ClaudeOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderReadinessKind.Ready, row.Kind);
        Assert.Equal("Recent normalized Claude usage is available.", row.Description);
    }

    [Fact]
    public void ClaudeSelectedWithStaleBridgeSnapshotIsStale()
    {
        var provider = new ProviderReadinessProvider(CreateStore(
            CreateSnapshot(ProviderKind.Claude, ProviderConnectionState.Connected, DataFreshness.Stale)));

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.ClaudeOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderReadinessKind.Stale, row.Kind);
        Assert.Equal("Claude usage data is older than the freshness window.", row.Description);
    }

    [Fact]
    public void ClaudeSelectedWithMalformedBridgeSnapshotIsUnavailable()
    {
        var provider = new ProviderReadinessProvider(CreateStore(
            CreateSnapshot(ProviderKind.Claude, ProviderConnectionState.Disconnected, DataFreshness.Unknown)));

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.ClaudeOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderReadinessKind.Unavailable, row.Kind);
        Assert.Equal("Claude usage is unavailable.", row.Description);
    }

    [Fact]
    public void CombinedModeShowsBothProviders()
    {
        var provider = new ProviderReadinessProvider(snapshotStore: null);

        var rows = provider.CreateReadinessRows(
            ProviderSelectionMode.Both,
            CodexRuntimeMode.WslLoginShell);

        Assert.Equal([ProviderKind.Codex, ProviderKind.Claude], rows.Select(row => row.Provider));
    }

    [Fact]
    public void CodexOnlyOmitsClaudeReadiness()
    {
        var provider = new ProviderReadinessProvider(snapshotStore: null);

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.CodexOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderKind.Codex, row.Provider);
    }

    [Fact]
    public void ClaudeOnlyOmitsCodexReadiness()
    {
        var provider = new ProviderReadinessProvider(snapshotStore: null);

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.ClaudeOnly,
            CodexRuntimeMode.WslLoginShell));

        Assert.Equal(ProviderKind.Claude, row.Provider);
    }

    [Fact]
    public void InvalidCodexRuntimeMapsToUnavailableWithoutExceptionText()
    {
        var provider = new ProviderReadinessProvider(snapshotStore: null);

        var row = Assert.Single(provider.CreateReadinessRows(
            ProviderSelectionMode.CodexOnly,
            (CodexRuntimeMode)999));

        Assert.Equal(ProviderReadinessKind.Unavailable, row.Kind);
        Assert.DoesNotContain("Exception", row.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", row.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static InMemoryProviderRuntimeSnapshotStore CreateStore(ProviderUsageSnapshot snapshot)
    {
        var store = new InMemoryProviderRuntimeSnapshotStore(
            TimeProvider.System,
            TimeSpan.FromMinutes(5));
        store.Store([snapshot]);
        return store;
    }

    private static ProviderUsageSnapshot CreateSnapshot(
        ProviderKind provider,
        ProviderConnectionState connectionState,
        DataFreshness freshness)
    {
        var metric = freshness == DataFreshness.Unknown
            ? PercentageUsageMetric.Unavailable(DataAuthority.LocalProviderReported, freshness)
            : new PercentageUsageMetric(25m, DataAuthority.LocalProviderReported, freshness);

        return new ProviderUsageSnapshot(
            provider,
            connectionState,
            metric,
            null,
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            TokenCountMetric.Unavailable(DataAuthority.TokenFishDerived, DataFreshness.Unknown),
            DateTimeOffset.UtcNow);
    }
}
