using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public sealed class ProviderUsageCollectionCoordinator
{
    private static readonly ProviderKind[] ProviderOrder =
    [
        ProviderKind.Claude,
        ProviderKind.Codex
    ];

    private readonly IReadOnlyDictionary<ProviderKind, IProviderUsageCollector> _collectorsByProvider;

    public ProviderUsageCollectionCoordinator(IEnumerable<IProviderUsageCollector> collectors)
    {
        ArgumentNullException.ThrowIfNull(collectors);

        var collectorsByProvider = new Dictionary<ProviderKind, IProviderUsageCollector>();

        foreach (var collector in collectors)
        {
            ArgumentNullException.ThrowIfNull(collector);

            if (!collectorsByProvider.TryAdd(collector.Provider, collector))
            {
                throw new InvalidOperationException(
                    $"Duplicate usage collector registered for provider '{collector.Provider}'.");
            }
        }

        _collectorsByProvider = collectorsByProvider;
    }

    public Task<IReadOnlyList<ProviderUsageSnapshot>> CollectAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return CollectAsync(settings.ProviderSelectionMode, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderUsageSnapshot>> CollectAsync(
        ProviderSelectionMode providerSelectionMode,
        CancellationToken cancellationToken)
    {
        var results = await CollectWithOutcomesAsync(providerSelectionMode, cancellationToken)
            .ConfigureAwait(false);

        return results.Select(result => result.Snapshot).ToArray();
    }

    public Task<IReadOnlyList<ProviderCollectionResult>> CollectWithOutcomesAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return CollectWithOutcomesAsync(settings.ProviderSelectionMode, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderCollectionResult>> CollectWithOutcomesAsync(
        ProviderSelectionMode providerSelectionMode,
        CancellationToken cancellationToken)
    {
        var enabledProviders = GetEnabledProviders(providerSelectionMode);
        var results = new List<ProviderCollectionResult>(enabledProviders.Length);

        foreach (var provider in enabledProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_collectorsByProvider.TryGetValue(provider, out var collector))
            {
                throw new InvalidOperationException(
                    $"No usage collector registered for enabled provider '{provider}'.");
            }

            var result = await collector.CollectWithOutcomeAsync(cancellationToken);
            var snapshot = result.Snapshot;

            if (snapshot.Provider != provider)
            {
                throw new InvalidOperationException(
                    $"Usage collector for provider '{provider}' returned a snapshot for provider '{snapshot.Provider}'.");
            }

            results.Add(result);
        }

        return results;
    }

    private static ProviderKind[] GetEnabledProviders(ProviderSelectionMode providerSelectionMode) =>
        providerSelectionMode switch
        {
            ProviderSelectionMode.ClaudeOnly => [ProviderKind.Claude],
            ProviderSelectionMode.CodexOnly => [ProviderKind.Codex],
            ProviderSelectionMode.Both => ProviderOrder,
            _ => throw new ArgumentOutOfRangeException(nameof(providerSelectionMode), providerSelectionMode, null)
        };
}
