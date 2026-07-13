using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public interface IProviderUsageCollector
{
    ProviderKind Provider { get; }

    Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken);

    async Task<ProviderCollectionResult> CollectWithOutcomeAsync(
        CancellationToken cancellationToken) =>
        new(
            await CollectAsync(cancellationToken).ConfigureAwait(false),
            ProviderCollectionOutcome.Succeeded);
}
