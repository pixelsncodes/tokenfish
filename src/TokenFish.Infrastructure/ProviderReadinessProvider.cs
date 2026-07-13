using TokenFish.Core.Models;
using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure;

public interface IProviderReadinessProvider
{
    IReadOnlyList<ProviderReadinessDisplayState> CreateReadinessRows(
        ProviderSelectionMode providerSelectionMode,
        CodexRuntimeMode codexRuntimeMode);
}

public sealed class ProviderReadinessProvider : IProviderReadinessProvider
{
    private readonly IProviderRuntimeSnapshotStore? _snapshotStore;

    public ProviderReadinessProvider(IProviderRuntimeSnapshotStore? snapshotStore)
    {
        _snapshotStore = snapshotStore;
    }

    public IReadOnlyList<ProviderReadinessDisplayState> CreateReadinessRows(
        ProviderSelectionMode providerSelectionMode,
        CodexRuntimeMode codexRuntimeMode)
    {
        var rows = new List<ProviderReadinessDisplayState>();

        if (providerSelectionMode is ProviderSelectionMode.CodexOnly or ProviderSelectionMode.Both)
        {
            rows.Add(CreateCodexRow(codexRuntimeMode));
        }

        if (providerSelectionMode is ProviderSelectionMode.ClaudeOnly or ProviderSelectionMode.Both)
        {
            rows.Add(CreateClaudeRow());
        }

        return rows;
    }

    private ProviderReadinessDisplayState CreateCodexRow(CodexRuntimeMode codexRuntimeMode)
    {
        if (!Enum.IsDefined(codexRuntimeMode))
        {
            return new ProviderReadinessDisplayState(
                ProviderKind.Codex,
                "Codex",
                ProviderReadinessKind.Unavailable,
                "Unavailable",
                "Codex runtime settings are not valid.");
        }

        return CreateSnapshotBackedRow(
            ProviderKind.Codex,
            "Codex",
            waitingDescription: "Codex usage will be checked after TokenFish restarts.",
            readyDescription: "Recent normalized Codex usage is available.",
            staleDescription: "Codex usage data is older than the freshness window.",
            unavailableDescription: "Codex usage is unavailable.");
    }

    private ProviderReadinessDisplayState CreateClaudeRow() =>
        CreateSnapshotBackedRow(
            ProviderKind.Claude,
            "Claude",
            waitingDescription: "Waiting for Claude Code to send usage through the local bridge.",
            readyDescription: "Recent normalized Claude usage is available.",
            staleDescription: "Claude usage data is older than the freshness window.",
            unavailableDescription: "Claude usage is unavailable.");

    private ProviderReadinessDisplayState CreateSnapshotBackedRow(
        ProviderKind provider,
        string providerName,
        string waitingDescription,
        string readyDescription,
        string staleDescription,
        string unavailableDescription)
    {
        if (_snapshotStore is null ||
            !_snapshotStore.TryGetCurrent(provider, out var state))
        {
            return new ProviderReadinessDisplayState(
                provider,
                providerName,
                ProviderReadinessKind.WaitingForData,
                "Waiting for data",
                waitingDescription);
        }

        if (state.Snapshot.ConnectionState is ProviderConnectionState.Disconnected
            or ProviderConnectionState.NotConfigured)
        {
            return new ProviderReadinessDisplayState(
                provider,
                providerName,
                ProviderReadinessKind.Unavailable,
                "Unavailable",
                unavailableDescription);
        }

        return state.EffectiveFreshness switch
        {
            DataFreshness.Live or DataFreshness.Cached => new ProviderReadinessDisplayState(
                provider,
                providerName,
                ProviderReadinessKind.Ready,
                "Ready",
                readyDescription),
            DataFreshness.Stale => new ProviderReadinessDisplayState(
                provider,
                providerName,
                ProviderReadinessKind.Stale,
                "Stale",
                staleDescription),
            _ => new ProviderReadinessDisplayState(
                provider,
                providerName,
                ProviderReadinessKind.WaitingForData,
                "Waiting for data",
                waitingDescription)
        };
    }
}

public sealed record ProviderReadinessDisplayState(
    ProviderKind Provider,
    string ProviderName,
    ProviderReadinessKind Kind,
    string Label,
    string Description);

public enum ProviderReadinessKind
{
    Ready,
    WaitingForData,
    Stale,
    Unavailable
}
