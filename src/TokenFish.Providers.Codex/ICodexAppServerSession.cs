using TokenFish.Core.Models;

namespace TokenFish.Providers.Codex;

public interface ICodexAppServerSession : IAsyncDisposable
{
    bool IsHealthy => true;

    Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken);
}
