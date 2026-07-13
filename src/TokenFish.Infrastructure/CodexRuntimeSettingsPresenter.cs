using TokenFish.Core.Models;

namespace TokenFish.Infrastructure;

public sealed class CodexRuntimeSettingsPresenter
{
    private const string ClaudeBridgeDescription =
        "Claude usage arrives through the local TokenFish Claude bridge.";
    private const string CodexSettingsRetainedDescription =
        "Codex runtime settings are retained for later use.";

    private static readonly IReadOnlyList<ProviderSelectionModeOption> ProviderSelectionOptions =
    [
        new(
            ProviderSelectionMode.CodexOnly,
            "Codex",
            "Reads usage through the configured Codex runtime."),
        new(
            ProviderSelectionMode.ClaudeOnly,
            "Claude",
            "Reads normalized usage received through the local TokenFish Claude bridge."),
        new(
            ProviderSelectionMode.Both,
            "Codex and Claude",
            "Shows both providers.")
    ];

    private static readonly IReadOnlyList<CodexRuntimeModeOption> RuntimeModeOptions =
    [
        new(CodexRuntimeMode.WslLoginShell, "WSL login shell"),
        new(CodexRuntimeMode.Wsl, "WSL direct"),
        new(CodexRuntimeMode.NativeWindows, "Native Windows")
    ];

    private readonly CodexRuntimeSettingsEditor _editor;
    private readonly IProviderReadinessProvider _readinessProvider;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public CodexRuntimeSettingsPresenter(
        CodexRuntimeSettingsEditor editor,
        IProviderReadinessProvider? readinessProvider = null)
    {
        ArgumentNullException.ThrowIfNull(editor);

        _editor = editor;
        _readinessProvider = readinessProvider ?? new ProviderReadinessProvider(null);
        State = CreateState(
            ProviderSelectionMode.CodexOnly,
            CodexRuntimeMode.WslLoginShell,
            null,
            string.Empty,
            CodexRuntimeSettingsStatusKind.Information,
            CodexRuntimeSettingsFocusTarget.None,
            isStatusVisible: false,
            isSaving: false);
    }

    public CodexRuntimeSettingsViewState State { get; private set; }

    public async Task<CodexRuntimeSettingsViewState> LoadAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var draft = await _editor.LoadDraftAsync(cancellationToken).ConfigureAwait(false);
            State = CreateState(
                draft.ProviderSelectionMode,
                draft.RuntimeMode,
                draft.WslDistributionName,
                string.Empty,
                CodexRuntimeSettingsStatusKind.Information,
                CodexRuntimeSettingsFocusTarget.None,
                isStatusVisible: false,
                isSaving: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            State = State with
            {
                StatusMessage = "Settings could not be loaded.",
                StatusKind = CodexRuntimeSettingsStatusKind.PersistenceError,
                FocusTarget = CodexRuntimeSettingsFocusTarget.None,
                IsStatusVisible = true,
                IsSaving = false
            };
        }

        return State;
    }

    public CodexRuntimeSettingsViewState SelectProviderSelectionMode(
        ProviderSelectionMode providerSelectionMode)
    {
        if (!Enum.IsDefined(providerSelectionMode))
        {
            State = CreateState(
                providerSelectionMode,
                State.RuntimeMode,
                State.WslDistributionName,
                "Select a supported provider.",
                CodexRuntimeSettingsStatusKind.ValidationError,
                CodexRuntimeSettingsFocusTarget.ProviderSelection,
                isStatusVisible: true,
                State.IsSaving);
            return State;
        }

        State = CreateState(
            providerSelectionMode,
            State.RuntimeMode,
            State.WslDistributionName,
            string.Empty,
            CodexRuntimeSettingsStatusKind.Information,
            CodexRuntimeSettingsFocusTarget.None,
            isStatusVisible: false,
            State.IsSaving);

        return State;
    }

    public CodexRuntimeSettingsViewState SelectRuntimeMode(CodexRuntimeMode runtimeMode)
    {
        if (!Enum.IsDefined(runtimeMode))
        {
            State = CreateState(
                State.ProviderSelectionMode,
                runtimeMode,
                State.WslDistributionName,
                "Select a supported runtime mode.",
                CodexRuntimeSettingsStatusKind.ValidationError,
                CodexRuntimeSettingsFocusTarget.RuntimeMode,
                isStatusVisible: true,
                State.IsSaving);
            return State;
        }

        State = CreateState(
            State.ProviderSelectionMode,
            runtimeMode,
            runtimeMode == CodexRuntimeMode.NativeWindows ? null : State.WslDistributionName,
            string.Empty,
            CodexRuntimeSettingsStatusKind.Information,
            CodexRuntimeSettingsFocusTarget.None,
            isStatusVisible: false,
            State.IsSaving);

        return State;
    }

    public CodexRuntimeSettingsViewState SetWslDistributionName(string? distributionName)
    {
        State = State with
        {
            WslDistributionName = distributionName ?? string.Empty
        };

        return State;
    }

    public async Task<CodexRuntimeSettingsViewState> SaveAsync(
        CancellationToken cancellationToken)
    {
        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return State;
        }

        try
        {
            State = State with
            {
                CanSave = false,
                IsSaving = true
            };

            var draft = new CodexRuntimeSettingsDraft(
                State.ProviderSelectionMode,
                State.RuntimeMode,
                State.IsWslDistributionEnabled ? State.WslDistributionName : null);
            var result = await _editor.SaveAsync(draft, cancellationToken).ConfigureAwait(false);

            State = result.Status switch
            {
                CodexRuntimeSettingsSaveStatus.Unchanged => State with
                {
                    StatusMessage = "Settings are already up to date.",
                    StatusKind = CodexRuntimeSettingsStatusKind.Information,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.None,
                    IsStatusVisible = true,
                    CanSave = true,
                    IsSaving = false
                },
                CodexRuntimeSettingsSaveStatus.Saved => State with
                {
                    StatusMessage = CreateRestartMessage(result.ProviderChanged, result.RuntimeChanged),
                    StatusKind = CodexRuntimeSettingsStatusKind.Success,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.None,
                    IsStatusVisible = true,
                    CanSave = true,
                    IsSaving = false
                },
                CodexRuntimeSettingsSaveStatus.ValidationFailed => State with
                {
                    StatusMessage = "Settings are not valid.",
                    StatusKind = CodexRuntimeSettingsStatusKind.ValidationError,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.RuntimeMode,
                    IsStatusVisible = true,
                    CanSave = true,
                    IsSaving = false
                },
                CodexRuntimeSettingsSaveStatus.PersistenceFailed => State with
                {
                    StatusMessage = "Settings could not be saved.",
                    StatusKind = CodexRuntimeSettingsStatusKind.PersistenceError,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.None,
                    IsStatusVisible = true,
                    CanSave = true,
                    IsSaving = false
                },
                _ => State with
                {
                    StatusMessage = "Settings could not be saved.",
                    StatusKind = CodexRuntimeSettingsStatusKind.PersistenceError,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.None,
                    IsStatusVisible = true,
                    CanSave = true,
                    IsSaving = false
                }
            };
        }
        finally
        {
            _saveGate.Release();
        }

        return State;
    }

    private CodexRuntimeSettingsViewState CreateState(
        ProviderSelectionMode providerSelectionMode,
        CodexRuntimeMode runtimeMode,
        string? distributionName,
        string statusMessage,
        CodexRuntimeSettingsStatusKind statusKind,
        CodexRuntimeSettingsFocusTarget focusTarget,
        bool isStatusVisible,
        bool isSaving)
    {
        var isCodexEnabled = providerSelectionMode is
            ProviderSelectionMode.CodexOnly or ProviderSelectionMode.Both;
        var isClaudeEnabled = providerSelectionMode is
            ProviderSelectionMode.ClaudeOnly or ProviderSelectionMode.Both;
        var isRuntimeModeEnabled = isCodexEnabled && !isSaving;
        var isWslDistributionEnabled =
            isRuntimeModeEnabled &&
            runtimeMode is CodexRuntimeMode.Wsl or CodexRuntimeMode.WslLoginShell;

        return
        new(
            ProviderSelectionOptions,
            providerSelectionMode,
            RuntimeModeOptions,
            runtimeMode,
            distributionName ?? string.Empty,
            isRuntimeModeEnabled,
            isWslDistributionEnabled,
            !isCodexEnabled,
            CodexSettingsRetainedDescription,
            isClaudeEnabled,
            ClaudeBridgeDescription,
            _readinessProvider.CreateReadinessRows(providerSelectionMode, runtimeMode),
            !isSaving,
            isSaving,
            statusMessage,
            statusKind,
            isStatusVisible,
            focusTarget);
    }

    private static string CreateRestartMessage(bool providerChanged, bool runtimeChanged)
    {
        if (providerChanged && runtimeChanged)
        {
            return "Provider and runtime changes take effect after TokenFish restarts.";
        }

        if (providerChanged)
        {
            return "Provider changes take effect after TokenFish restarts.";
        }

        return "Runtime changes take effect after TokenFish restarts.";
    }
}

public sealed record ProviderSelectionModeOption(
    ProviderSelectionMode ProviderSelectionMode,
    string Label,
    string Description);

public sealed record CodexRuntimeModeOption(CodexRuntimeMode RuntimeMode, string Label);

public sealed record CodexRuntimeSettingsViewState(
    IReadOnlyList<ProviderSelectionModeOption> ProviderSelectionOptions,
    ProviderSelectionMode ProviderSelectionMode,
    IReadOnlyList<CodexRuntimeModeOption> RuntimeModeOptions,
    CodexRuntimeMode RuntimeMode,
    string WslDistributionName,
    bool IsRuntimeModeEnabled,
    bool IsWslDistributionEnabled,
    bool IsCodexSettingsRetainedMessageVisible,
    string CodexSettingsRetainedMessage,
    bool IsClaudeBridgeDescriptionVisible,
    string ClaudeBridgeDescription,
    IReadOnlyList<ProviderReadinessDisplayState> ReadinessRows,
    bool CanSave,
    bool IsSaving,
    string StatusMessage,
    CodexRuntimeSettingsStatusKind StatusKind,
    bool IsStatusVisible,
    CodexRuntimeSettingsFocusTarget FocusTarget);

public enum CodexRuntimeSettingsStatusKind
{
    Information,
    Success,
    ValidationError,
    PersistenceError
}

public enum CodexRuntimeSettingsFocusTarget
{
    None,
    ProviderSelection,
    RuntimeMode,
    WslDistribution
}
