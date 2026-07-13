using System.Collections.ObjectModel;
using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public sealed class InMemoryProviderRuntimeSnapshotStore : IProviderRuntimeSnapshotStore
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _freshnessThreshold;
    private readonly object _sync = new();
    private readonly Dictionary<ProviderKind, StoredSnapshot> _snapshotsByProvider = [];

    public InMemoryProviderRuntimeSnapshotStore(
        TimeProvider timeProvider,
        TimeSpan freshnessThreshold)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (freshnessThreshold < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(freshnessThreshold),
                "Freshness threshold cannot be negative.");
        }

        _timeProvider = timeProvider;
        _freshnessThreshold = freshnessThreshold;
    }

    public void Store(IReadOnlyList<ProviderUsageSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        Store(snapshots.Select(snapshot => new ProviderCollectionResult(
            snapshot,
            ProviderCollectionOutcome.Succeeded)).ToArray());
    }

    public void Store(IReadOnlyList<ProviderCollectionResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var acceptedAt = _timeProvider.GetUtcNow().ToUniversalTime();

        lock (_sync)
        {
            foreach (var result in results)
            {
                ArgumentNullException.ThrowIfNull(result);
                var snapshot = result.Snapshot;
                ArgumentNullException.ThrowIfNull(snapshot);

                _snapshotsByProvider[snapshot.Provider] = new StoredSnapshot(
                    snapshot,
                    acceptedAt,
                    result.Outcome,
                    result.FailureReason);
            }
        }
    }

    public bool TryGetCurrent(
        ProviderKind provider,
        out ProviderRuntimeSnapshotState currentState)
    {
        StoredSnapshot storedSnapshot;

        lock (_sync)
        {
            if (!_snapshotsByProvider.TryGetValue(provider, out storedSnapshot!))
            {
                currentState = null!;
                return false;
            }
        }

        currentState = CreateState(storedSnapshot);
        return true;
    }

    public IReadOnlyDictionary<ProviderKind, ProviderRuntimeSnapshotState> GetCurrentSnapshots()
    {
        StoredSnapshot[] storedSnapshots;

        lock (_sync)
        {
            storedSnapshots = _snapshotsByProvider.Values.ToArray();
        }

        var currentSnapshots = storedSnapshots.ToDictionary(
            storedSnapshot => storedSnapshot.Snapshot.Provider,
            CreateState);

        return new ReadOnlyDictionary<ProviderKind, ProviderRuntimeSnapshotState>(
            currentSnapshots);
    }

    private ProviderRuntimeSnapshotState CreateState(StoredSnapshot storedSnapshot) =>
        new(
            storedSnapshot.Snapshot,
            storedSnapshot.AcceptedAt,
            GetEffectiveFreshness(storedSnapshot),
            storedSnapshot.CollectionOutcome,
            storedSnapshot.CollectionFailureReason);

    private DataFreshness GetEffectiveFreshness(StoredSnapshot storedSnapshot)
    {
        if (!HasAvailableKnownData(storedSnapshot.Snapshot))
        {
            return DataFreshness.Unknown;
        }

        if (HasProviderReportedStaleData(storedSnapshot.Snapshot))
        {
            return DataFreshness.Stale;
        }

        var age = _timeProvider.GetUtcNow().ToUniversalTime() -
            GetFreshnessOrigin(storedSnapshot);

        return age <= _freshnessThreshold
            ? DataFreshness.Live
            : DataFreshness.Stale;
    }

    private static DateTimeOffset GetFreshnessOrigin(StoredSnapshot storedSnapshot)
    {
        var sourceObservedAt = storedSnapshot.Snapshot.SourceObservedAt;
        return sourceObservedAt is { } timestamp &&
            timestamp != DateTimeOffset.MinValue &&
            timestamp <= storedSnapshot.AcceptedAt
            ? timestamp
            : storedSnapshot.AcceptedAt;
    }

    private static bool HasAvailableKnownData(ProviderUsageSnapshot snapshot) =>
        IsAvailableKnown(snapshot.UsageWindow) ||
        IsAvailableKnown(snapshot.SessionTokens) ||
        IsAvailableKnown(snapshot.WeeklyTokens) ||
        snapshot.QuotaWindows.Any(IsAvailableKnown) ||
        snapshot.ActivityMetrics.Any(IsAvailableKnown);

    private static bool HasProviderReportedStaleData(ProviderUsageSnapshot snapshot) =>
        IsAvailableStale(snapshot.UsageWindow) ||
        IsAvailableStale(snapshot.SessionTokens) ||
        IsAvailableStale(snapshot.WeeklyTokens) ||
        snapshot.QuotaWindows.Any(IsAvailableStale) ||
        snapshot.ActivityMetrics.Any(IsAvailableStale);

    private static bool IsAvailableKnown(PercentageUsageMetric metric) =>
        metric.IsAvailable && metric.Freshness != DataFreshness.Unknown;

    private static bool IsAvailableKnown(TokenCountMetric metric) =>
        metric.IsAvailable && metric.Freshness != DataFreshness.Unknown;

    private static bool IsAvailableStale(PercentageUsageMetric metric) =>
        metric.IsAvailable && metric.Freshness == DataFreshness.Stale;

    private static bool IsAvailableStale(TokenCountMetric metric) =>
        metric.IsAvailable && metric.Freshness == DataFreshness.Stale;

    private static bool IsAvailableKnown(NormalizedQuotaWindow window) =>
        window.IsAvailable && window.Freshness != DataFreshness.Unknown;

    private static bool IsAvailableKnown(NormalizedActivityMetric metric) =>
        metric.IsAvailable && metric.Freshness != DataFreshness.Unknown;

    private static bool IsAvailableStale(NormalizedQuotaWindow window) =>
        window.IsAvailable && window.Freshness == DataFreshness.Stale;

    private static bool IsAvailableStale(NormalizedActivityMetric metric) =>
        metric.IsAvailable && metric.Freshness == DataFreshness.Stale;

    private sealed record StoredSnapshot(
        ProviderUsageSnapshot Snapshot,
        DateTimeOffset AcceptedAt,
        ProviderCollectionOutcome CollectionOutcome,
        ProviderCollectionFailureReason? CollectionFailureReason);
}
