using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Core.Tests;

public sealed class ProviderUsageCollectionCoordinatorTests
{
    [Fact]
    public async Task ClaudeOnlyModeInvokesClaudeAndNotCodex()
    {
        var claudeCollector = new RecordingProviderUsageCollector(ProviderKind.Claude);
        var codexCollector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var coordinator = new ProviderUsageCollectionCoordinator([claudeCollector, codexCollector]);

        var snapshots = await coordinator.CollectAsync(ProviderSelectionMode.ClaudeOnly, CancellationToken.None);

        Assert.Single(snapshots);
        Assert.Equal(ProviderKind.Claude, snapshots[0].Provider);
        Assert.Equal(1, claudeCollector.CallCount);
        Assert.Equal(0, codexCollector.CallCount);
    }

    [Fact]
    public async Task CodexOnlyModeInvokesCodexAndNotClaude()
    {
        var claudeCollector = new RecordingProviderUsageCollector(ProviderKind.Claude);
        var codexCollector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var coordinator = new ProviderUsageCollectionCoordinator([claudeCollector, codexCollector]);

        var snapshots = await coordinator.CollectAsync(ProviderSelectionMode.CodexOnly, CancellationToken.None);

        Assert.Single(snapshots);
        Assert.Equal(ProviderKind.Codex, snapshots[0].Provider);
        Assert.Equal(0, claudeCollector.CallCount);
        Assert.Equal(1, codexCollector.CallCount);
    }

    [Fact]
    public async Task BothModeInvokesBothCollectorsExactlyOnce()
    {
        var claudeCollector = new RecordingProviderUsageCollector(ProviderKind.Claude);
        var codexCollector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var coordinator = new ProviderUsageCollectionCoordinator([claudeCollector, codexCollector]);

        await coordinator.CollectAsync(ProviderSelectionMode.Both, CancellationToken.None);

        Assert.Equal(1, claudeCollector.CallCount);
        Assert.Equal(1, codexCollector.CallCount);
    }

    [Fact]
    public async Task BothModeReturnsSnapshotsInProviderOrder()
    {
        var claudeCollector = new RecordingProviderUsageCollector(ProviderKind.Claude);
        var codexCollector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var coordinator = new ProviderUsageCollectionCoordinator([codexCollector, claudeCollector]);

        var snapshots = await coordinator.CollectAsync(ProviderSelectionMode.Both, CancellationToken.None);

        Assert.Equal(
            new[] { ProviderKind.Claude, ProviderKind.Codex },
            snapshots.Select(snapshot => snapshot.Provider));
    }

    [Fact]
    public async Task DisabledUnregisteredProviderDoesNotCauseFailure()
    {
        var claudeCollector = new RecordingProviderUsageCollector(ProviderKind.Claude);
        var coordinator = new ProviderUsageCollectionCoordinator([claudeCollector]);

        var snapshots = await coordinator.CollectAsync(ProviderSelectionMode.ClaudeOnly, CancellationToken.None);

        Assert.Single(snapshots);
        Assert.Equal(ProviderKind.Claude, snapshots[0].Provider);
        Assert.Equal(1, claudeCollector.CallCount);
    }

    [Fact]
    public async Task EnabledUnregisteredProviderCausesClearException()
    {
        var coordinator = new ProviderUsageCollectionCoordinator([]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.CollectAsync(ProviderSelectionMode.ClaudeOnly, CancellationToken.None));

        Assert.Contains("No usage collector registered", exception.Message);
        Assert.Contains("Claude", exception.Message);
    }

    [Fact]
    public void DuplicateCollectorsForSameProviderAreRejected()
    {
        var collectors = new[]
        {
            new RecordingProviderUsageCollector(ProviderKind.Codex),
            new RecordingProviderUsageCollector(ProviderKind.Codex)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ProviderUsageCollectionCoordinator(collectors));

        Assert.Contains("Duplicate usage collector", exception.Message);
        Assert.Contains("Codex", exception.Message);
    }

    [Fact]
    public async Task CancellationIsPassedToCollectorsAndPropagated()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var collector = new RecordingProviderUsageCollector(
            ProviderKind.Claude,
            cancellationToken =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromCanceled<ProviderUsageSnapshot>(cancellationToken);
            });
        var coordinator = new ProviderUsageCollectionCoordinator([collector]);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            coordinator.CollectAsync(ProviderSelectionMode.ClaudeOnly, cancellationTokenSource.Token));
        Assert.Equal(cancellationTokenSource.Token, collector.LastCancellationToken);
        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task CollectorExceptionsArePropagated()
    {
        var expectedException = new InvalidOperationException("collector failed");
        var collector = new RecordingProviderUsageCollector(
            ProviderKind.Codex,
            _ => Task.FromException<ProviderUsageSnapshot>(expectedException));
        var coordinator = new ProviderUsageCollectionCoordinator([collector]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.CollectAsync(ProviderSelectionMode.CodexOnly, CancellationToken.None));

        Assert.Same(expectedException, exception);
        Assert.Equal(1, collector.CallCount);
    }

    [Fact]
    public async Task CollectorCannotReturnSnapshotForDifferentProvider()
    {
        var collector = new RecordingProviderUsageCollector(
            ProviderKind.Claude,
            _ => Task.FromResult(RecordingProviderUsageCollector.CreateSnapshot(ProviderKind.Codex)));
        var coordinator = new ProviderUsageCollectionCoordinator([collector]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.CollectAsync(ProviderSelectionMode.ClaudeOnly, CancellationToken.None));

        Assert.Contains("returned a snapshot", exception.Message);
        Assert.Contains("Claude", exception.Message);
        Assert.Contains("Codex", exception.Message);
    }

    [Fact]
    public async Task AppSettingsProviderSelectionIsSupported()
    {
        var codexCollector = new RecordingProviderUsageCollector(ProviderKind.Codex);
        var coordinator = new ProviderUsageCollectionCoordinator([codexCollector]);
        var settings = new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly };

        var snapshots = await coordinator.CollectAsync(settings, CancellationToken.None);

        Assert.Single(snapshots);
        Assert.Equal(ProviderKind.Codex, snapshots[0].Provider);
        Assert.Equal(1, codexCollector.CallCount);
    }

    private sealed class RecordingProviderUsageCollector : IProviderUsageCollector
    {
        private readonly Func<CancellationToken, Task<ProviderUsageSnapshot>> _collectAsync;

        public ProviderKind Provider { get; }

        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public RecordingProviderUsageCollector(
            ProviderKind provider,
            Func<CancellationToken, Task<ProviderUsageSnapshot>>? collectAsync = null)
        {
            Provider = provider;
            _collectAsync = collectAsync ?? (_ => Task.FromResult(CreateSnapshot(provider)));
        }

        public Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            LastCancellationToken = cancellationToken;

            return _collectAsync(cancellationToken);
        }

        public static ProviderUsageSnapshot CreateSnapshot(ProviderKind provider) =>
            new(
                provider,
                ProviderConnectionState.Connected,
                new PercentageUsageMetric(
                    50m,
                    DataAuthority.LocalProviderReported,
                    DataFreshness.Live),
                DateTimeOffset.UtcNow.AddHours(1),
                new TokenCountMetric(
                    100,
                    DataAuthority.LocalProviderReported,
                    DataFreshness.Live),
                new TokenCountMetric(
                    1_000,
                    DataAuthority.TokenFishDerived,
                    DataFreshness.Cached),
                DateTimeOffset.UtcNow);
    }
}
