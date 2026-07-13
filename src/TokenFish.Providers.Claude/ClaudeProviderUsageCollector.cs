using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Providers.Claude;

public sealed class ClaudeProviderUsageCollector : IProviderUsageCollector
{
    private static readonly TimeSpan DefaultFutureTimestampTolerance = TimeSpan.FromMinutes(2);

    private readonly IClaudeBridgeStateStore _stateStore;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _freshnessThreshold;
    private readonly TimeSpan _futureTimestampTolerance;
    private readonly ClaudeUsageSnapshotFactory _snapshotFactory;

    internal ClaudeProviderUsageCollector(
        IClaudeBridgeStateStore stateStore,
        TimeProvider timeProvider,
        TimeSpan freshnessThreshold,
        TimeSpan futureTimestampTolerance)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (freshnessThreshold < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(freshnessThreshold),
                "Freshness threshold cannot be negative.");
        }

        if (futureTimestampTolerance < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(futureTimestampTolerance),
                "Future timestamp tolerance cannot be negative.");
        }

        _stateStore = stateStore;
        _timeProvider = timeProvider;
        _freshnessThreshold = freshnessThreshold;
        _futureTimestampTolerance = futureTimestampTolerance;
        _snapshotFactory = new ClaudeUsageSnapshotFactory();
    }

    public ProviderKind Provider => ProviderKind.Claude;

    public static IProviderUsageCollector CreateDefault(
        TimeProvider timeProvider,
        TimeSpan freshnessThreshold)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var collector = new ClaudeProviderUsageCollector(
            new ClaudeBridgeStateFileStore(),
            timeProvider,
            freshnessThreshold,
            DefaultFutureTimestampTolerance);

        return new ClaudeBridgeFailureMappingCollector(collector, timeProvider);
    }

    public async Task<ProviderUsageSnapshot> CollectAsync(
        CancellationToken cancellationToken)
        => (await CollectWithOutcomeAsync(cancellationToken).ConfigureAwait(false)).Snapshot;

    public async Task<ProviderCollectionResult> CollectWithOutcomeAsync(
        CancellationToken cancellationToken)
    {
        var state = await _stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (state.LoadFailureKind is { } failureKind)
        {
            return new ProviderCollectionResult(
                ClaudeUsageSnapshotFactory.CreateUnavailable(_timeProvider.GetUtcNow()),
                ProviderCollectionOutcome.Failed,
                failureKind is ClaudeBridgeStateStoreFailureKind.Malformed
                    ? ProviderCollectionFailureReason.ClaudeBridgeMalformed
                    : ProviderCollectionFailureReason.ClaudeBridgeUnreadable);
        }

        var snapshot = _snapshotFactory.Create(
            state,
            _timeProvider.GetUtcNow(),
            _freshnessThreshold,
            _futureTimestampTolerance);

        return new ProviderCollectionResult(
            snapshot,
            snapshot.QuotaWindows.Count == 0
                ? ProviderCollectionOutcome.NoObservation
                : ProviderCollectionOutcome.Succeeded);
    }
}
