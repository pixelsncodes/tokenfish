using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex;

public interface ICodexAppServerProtocolClient
{
    Task<CodexRateLimitsSnapshot> ReadRateLimitsAsync(CancellationToken cancellationToken);

    Task<CodexAccountUsageSnapshot> ReadAccountUsageAsync(CancellationToken cancellationToken);
}
