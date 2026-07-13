using TokenFish.Core.Models;

namespace TokenFish.Infrastructure;

public sealed class CodexRuntimeSettingsPresenter
{
    private static readonly IReadOnlyList<CodexRuntimeModeOption> RuntimeModeOptions =
    [
        new(CodexRuntimeMode.WslLoginShell, "WSL login shell"),
        new(CodexRuntimeMode.Wsl, "WSL direct"),
        new(CodexRuntimeMode.NativeWindows, "Native Windows")
    ];

    private readonly CodexRuntimeSettingsEditor _editor;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public CodexRuntimeSettingsPresenter(CodexRuntimeSettingsEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        _editor = editor;
        State = CreateState(
            CodexRuntimeMode.WslLoginShell,
            null,
            "Loading settings...",
            CodexRuntimeSettingsStatusKind.Information,
            CodexRuntimeSettingsFocusTarget.None,
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
                draft.RuntimeMode,
                draft.WslDistributionName,
                RestartMessage,
                CodexRuntimeSettingsStatusKind.Information,
                CodexRuntimeSettingsFocusTarget.None,
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
                IsSaving = false
            };
        }

        return State;
    }

    public CodexRuntimeSettingsViewState SelectRuntimeMode(CodexRuntimeMode runtimeMode)
    {
        if (!Enum.IsDefined(runtimeMode))
        {
            State = CreateState(
                runtimeMode,
                State.WslDistributionName,
                "Select a supported runtime mode.",
                CodexRuntimeSettingsStatusKind.ValidationError,
                CodexRuntimeSettingsFocusTarget.RuntimeMode,
                State.IsSaving);
            return State;
        }

        State = CreateState(
            runtimeMode,
            runtimeMode == CodexRuntimeMode.NativeWindows ? null : State.WslDistributionName,
            RestartMessage,
            CodexRuntimeSettingsStatusKind.Information,
            CodexRuntimeSettingsFocusTarget.None,
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
                State.RuntimeMode,
                State.IsWslDistributionEnabled ? State.WslDistributionName : null);
            var result = await _editor.SaveAsync(draft, cancellationToken).ConfigureAwait(false);

            State = result.Status switch
            {
                CodexRuntimeSettingsSaveStatus.Saved => State with
                {
                    StatusMessage = "Settings saved. Runtime changes apply after TokenFish is restarted.",
                    StatusKind = CodexRuntimeSettingsStatusKind.Success,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.None,
                    CanSave = true,
                    IsSaving = false
                },
                CodexRuntimeSettingsSaveStatus.ValidationFailed => State with
                {
                    StatusMessage = "Settings are not valid.",
                    StatusKind = CodexRuntimeSettingsStatusKind.ValidationError,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.RuntimeMode,
                    CanSave = true,
                    IsSaving = false
                },
                CodexRuntimeSettingsSaveStatus.PersistenceFailed => State with
                {
                    StatusMessage = "Settings could not be saved.",
                    StatusKind = CodexRuntimeSettingsStatusKind.PersistenceError,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.None,
                    CanSave = true,
                    IsSaving = false
                },
                _ => State with
                {
                    StatusMessage = "Settings could not be saved.",
                    StatusKind = CodexRuntimeSettingsStatusKind.PersistenceError,
                    FocusTarget = CodexRuntimeSettingsFocusTarget.None,
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

    private static CodexRuntimeSettingsViewState CreateState(
        CodexRuntimeMode runtimeMode,
        string? distributionName,
        string statusMessage,
        CodexRuntimeSettingsStatusKind statusKind,
        CodexRuntimeSettingsFocusTarget focusTarget,
        bool isSaving) =>
        new(
            RuntimeModeOptions,
            runtimeMode,
            distributionName ?? string.Empty,
            runtimeMode is CodexRuntimeMode.Wsl or CodexRuntimeMode.WslLoginShell,
            !isSaving,
            isSaving,
            statusMessage,
            statusKind,
            focusTarget);

    private const string RestartMessage =
        "Saved runtime changes apply after TokenFish is restarted.";
}

public sealed record CodexRuntimeModeOption(CodexRuntimeMode RuntimeMode, string Label);

public sealed record CodexRuntimeSettingsViewState(
    IReadOnlyList<CodexRuntimeModeOption> RuntimeModeOptions,
    CodexRuntimeMode RuntimeMode,
    string WslDistributionName,
    bool IsWslDistributionEnabled,
    bool CanSave,
    bool IsSaving,
    string StatusMessage,
    CodexRuntimeSettingsStatusKind StatusKind,
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
    RuntimeMode,
    WslDistribution
}
