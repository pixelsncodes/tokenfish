using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Providers.Codex;

namespace TokenFish.Infrastructure;

public sealed class TokenFishApplicationServices : IAsyncDisposable
{
    private readonly IAsyncDisposable? _codexRuntimeOwner;

    private bool _disposed;

    private TokenFishApplicationServices(
        IProviderUsageCollector? codexUsageCollector,
        IAsyncDisposable? codexRuntimeOwner)
    {
        CodexUsageCollector = codexUsageCollector;
        _codexRuntimeOwner = codexRuntimeOwner;
    }

    public IProviderUsageCollector? CodexUsageCollector { get; }

    public static TokenFishApplicationServices Create(
        AppSettings settings,
        string clientVersion)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);

        return Create(
            settings,
            clientVersion,
            static (launchCommand, version) =>
            {
                var services = CodexRuntimeServices.Create(launchCommand, version);
                return new ProviderRuntime(services.CodexUsageCollector, services);
            });
    }

    internal static TokenFishApplicationServices Create(
        AppSettings settings,
        string clientVersion,
        Func<CodexAppServerLaunchCommand, string, ProviderRuntime> createCodexRuntime)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
        ArgumentNullException.ThrowIfNull(createCodexRuntime);

        if (!IsCodexEnabled(settings.ProviderSelectionMode))
        {
            return new TokenFishApplicationServices(null, null);
        }

        var launchCommand = CodexAppServerLaunchCommandFactory.Create(settings);
        var codexRuntime = createCodexRuntime(launchCommand, clientVersion);

        return new TokenFishApplicationServices(
            codexRuntime.Collector,
            codexRuntime.Owner);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_codexRuntimeOwner is not null)
        {
            await _codexRuntimeOwner.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool IsCodexEnabled(ProviderSelectionMode providerSelectionMode) =>
        providerSelectionMode switch
        {
            ProviderSelectionMode.CodexOnly or ProviderSelectionMode.Both => true,
            ProviderSelectionMode.ClaudeOnly => false,
            _ => throw new ArgumentOutOfRangeException(
                nameof(providerSelectionMode),
                "Provider selection mode is not supported.")
        };

    internal sealed record ProviderRuntime(
        IProviderUsageCollector Collector,
        IAsyncDisposable Owner);
}
