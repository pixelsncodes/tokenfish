using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex;

internal sealed class CodexRuntimeFailureMappingCollector : IProviderUsageCollector
{
    private readonly IProviderUsageCollector _innerCollector;
    private readonly TimeProvider _timeProvider;

    public CodexRuntimeFailureMappingCollector(
        IProviderUsageCollector innerCollector,
        TimeProvider? timeProvider = null)
    {
        _innerCollector = innerCollector ?? throw new ArgumentNullException(nameof(innerCollector));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ProviderKind Provider => ProviderKind.Codex;

    public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _innerCollector.CollectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsExpectedCodexRuntimeFailure(exception))
        {
            return CreateUnavailableSnapshot();
        }
    }

    private ProviderUsageSnapshot CreateUnavailableSnapshot() =>
        new(
            ProviderKind.Codex,
            ProviderConnectionState.Disconnected,
            PercentageUsageMetric.Unavailable(
                DataAuthority.LocalProviderReported,
                DataFreshness.Unknown),
            null,
            TokenCountMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            TokenCountMetric.Unavailable(
                DataAuthority.TokenFishDerived,
                DataFreshness.Unknown),
            _timeProvider.GetUtcNow());

    private static bool IsExpectedCodexRuntimeFailure(Exception exception) =>
        exception is CodexAppServerSessionException ||
        exception is CodexAppServerProtocolException ||
        exception is CodexRateLimitsResponseParseException ||
        exception is CodexAccountUsageResponseParseException ||
        exception is CodexUsageSnapshotNormalizationException;
}
