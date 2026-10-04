using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex;

public sealed class CodexProviderUsageCollector : IProviderUsageCollector
{
    private readonly ICodexAppServerProtocolClient _protocolClient;
    private readonly CodexUsageSnapshotFactory _snapshotFactory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _activityTimeout;

    public bool RequiresSessionRecovery { get; private set; }

    public CodexProviderUsageCollector(
        ICodexAppServerProtocolClient protocolClient,
        CodexUsageSnapshotFactory snapshotFactory,
        TimeProvider? timeProvider = null,
        TimeSpan? activityTimeout = null)
    {
        _protocolClient = protocolClient ?? throw new ArgumentNullException(nameof(protocolClient));
        _snapshotFactory = snapshotFactory ?? throw new ArgumentNullException(nameof(snapshotFactory));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _activityTimeout = activityTimeout ?? TimeSpan.FromSeconds(5);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_activityTimeout, TimeSpan.Zero);
    }

    public ProviderKind Provider => ProviderKind.Codex;

    public async Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        var rateLimits = await _protocolClient.ReadRateLimitsAsync(cancellationToken)
            .ConfigureAwait(false);
        CodexAccountUsageSnapshot accountUsage;
        using var activityDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        activityDeadline.CancelAfter(_activityTimeout);
        try
        {
            accountUsage = await _protocolClient.ReadAccountUsageAsync(activityDeadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (activityDeadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            RequiresSessionRecovery = true;
            accountUsage = new(null);
        }
        catch (Exception exception) when (exception is CodexAppServerProtocolException or CodexAccountUsageResponseParseException or IOException)
        {
            RequiresSessionRecovery = exception is not CodexAppServerProtocolException { Kind: CodexProtocolErrorKind.UnsupportedMethod };
            accountUsage = new(null);
        }
        var capturedAt = _timeProvider.GetUtcNow();

        return _snapshotFactory.Create(rateLimits, accountUsage, capturedAt);
    }
}
