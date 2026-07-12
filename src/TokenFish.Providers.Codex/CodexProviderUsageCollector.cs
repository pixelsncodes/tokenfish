using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Providers.Codex;

public sealed class CodexProviderUsageCollector : IProviderUsageCollector
{
    private readonly ICodexAppServerProtocolClient _protocolClient;
    private readonly CodexUsageSnapshotFactory _snapshotFactory;
    private readonly TimeProvider _timeProvider;

    public CodexProviderUsageCollector(
        ICodexAppServerProtocolClient protocolClient,
        CodexUsageSnapshotFactory snapshotFactory,
        TimeProvider? timeProvider = null)
    {
        _protocolClient = protocolClient ?? throw new ArgumentNullException(nameof(protocolClient));
        _snapshotFactory = snapshotFactory ?? throw new ArgumentNullException(nameof(snapshotFactory));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ProviderKind Provider => ProviderKind.Codex;

    public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        var rateLimits = await _protocolClient.ReadRateLimitsAsync(cancellationToken)
            .ConfigureAwait(false);
        var accountUsage = await _protocolClient.ReadAccountUsageAsync(cancellationToken)
            .ConfigureAwait(false);
        var capturedAt = _timeProvider.GetUtcNow();

        return _snapshotFactory.Create(rateLimits, accountUsage, capturedAt);
    }
}
