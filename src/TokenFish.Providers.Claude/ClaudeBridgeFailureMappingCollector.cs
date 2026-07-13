using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Providers.Claude;

internal sealed class ClaudeBridgeFailureMappingCollector : IProviderUsageCollector
{
    private readonly IProviderUsageCollector _innerCollector;
    private readonly TimeProvider _timeProvider;

    public ClaudeBridgeFailureMappingCollector(
        IProviderUsageCollector innerCollector,
        TimeProvider timeProvider)
    {
        _innerCollector = innerCollector ?? throw new ArgumentNullException(nameof(innerCollector));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public ProviderKind Provider => ProviderKind.Claude;

    public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _innerCollector.CollectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsExpectedClaudeBridgeFailure(exception))
        {
            return ClaudeUsageSnapshotFactory.CreateUnavailable(_timeProvider.GetUtcNow());
        }
    }

    private static bool IsExpectedClaudeBridgeFailure(Exception exception) =>
        exception is ClaudeBridgeStateStoreException ||
        exception is IOException ||
        exception is UnauthorizedAccessException;
}
