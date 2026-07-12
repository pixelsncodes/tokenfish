using TokenFish.Core.Models;

namespace TokenFish.Providers.Codex;

public interface ICodexAppServerSession : IAsyncDisposable
{
    Task<ProviderUsageSnapshot> CollectAsync(CancellationToken cancellationToken);
}
