using TokenFish.Core.Models;

namespace TokenFish.Core.Providers;

public interface IProviderUsageCollector
{
    ProviderKind Provider { get; }

    Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken);
}
