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
        => (await CollectWithOutcomeAsync(cancellationToken).ConfigureAwait(false)).Snapshot;

    public async Task<ProviderCollectionResult> CollectWithOutcomeAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _innerCollector.CollectWithOutcomeAsync(cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
        catch (Exception exception) when (IsExpectedClaudeBridgeFailure(exception))
        {
            return new ProviderCollectionResult(
                ClaudeUsageSnapshotFactory.CreateUnavailable(_timeProvider.GetUtcNow()),
                ProviderCollectionOutcome.Failed,
                GetFailureReason(exception));
        }
    }

    private static bool IsExpectedClaudeBridgeFailure(Exception exception) =>
        exception is ClaudeBridgeStateStoreException ||
        exception is IOException ||
        exception is UnauthorizedAccessException;

    private static ProviderCollectionFailureReason GetFailureReason(Exception exception) =>
        exception is ClaudeBridgeStateStoreException
        {
            FailureKind: ClaudeBridgeStateStoreFailureKind.Malformed
        }
            ? ProviderCollectionFailureReason.ClaudeBridgeMalformed
            : ProviderCollectionFailureReason.ClaudeBridgeUnreadable;
}
