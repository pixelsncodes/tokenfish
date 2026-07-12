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

        var acceptedAt = _timeProvider.GetUtcNow().ToUniversalTime();

        lock (_sync)
        {
            foreach (var snapshot in snapshots)
            {
                ArgumentNullException.ThrowIfNull(snapshot);

                _snapshotsByProvider[snapshot.Provider] = new StoredSnapshot(
                    snapshot,
                    acceptedAt);
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
            GetEffectiveFreshness(storedSnapshot));

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

        var age = _timeProvider.GetUtcNow().ToUniversalTime() - storedSnapshot.AcceptedAt;

        return age <= _freshnessThreshold
            ? DataFreshness.Live
            : DataFreshness.Stale;
    }

    private static bool HasAvailableKnownData(ProviderUsageSnapshot snapshot) =>
        IsAvailableKnown(snapshot.UsageWindow) ||
        IsAvailableKnown(snapshot.SessionTokens) ||
        IsAvailableKnown(snapshot.WeeklyTokens);

    private static bool HasProviderReportedStaleData(ProviderUsageSnapshot snapshot) =>
        IsAvailableStale(snapshot.UsageWindow) ||
        IsAvailableStale(snapshot.SessionTokens) ||
        IsAvailableStale(snapshot.WeeklyTokens);

    private static bool IsAvailableKnown(PercentageUsageMetric metric) =>
        metric.IsAvailable && metric.Freshness != DataFreshness.Unknown;

    private static bool IsAvailableKnown(TokenCountMetric metric) =>
        metric.IsAvailable && metric.Freshness != DataFreshness.Unknown;

    private static bool IsAvailableStale(PercentageUsageMetric metric) =>
        metric.IsAvailable && metric.Freshness == DataFreshness.Stale;

    private static bool IsAvailableStale(TokenCountMetric metric) =>
        metric.IsAvailable && metric.Freshness == DataFreshness.Stale;

    private sealed record StoredSnapshot(
        ProviderUsageSnapshot Snapshot,
        DateTimeOffset AcceptedAt);
}
