using TokenFish.Core.Models;
using TokenFish.Core.Settings;

namespace TokenFish.Infrastructure;

public sealed class CodexRuntimeSettingsPresenter
{
    private const string ClaudeBridgeDescription =
        "Claude usage arrives through the local TokenFish Claude bridge.";
    private const string ClaudeSetupDescription =
        "Claude usage reaches TokenFish through this local bridge. Add this statusLine object to your Claude Code user settings.";
    private const string ClaudeSetupNextSteps =
        "Next steps: save Claude or Codex and Claude as the TokenFish provider selection, restart TokenFish when requested, configure Claude Code manually, trigger a normal Claude Code status-line update, then refresh TokenFish.";
    private const string ClaudeSetupFolderMoveNote =
        "If you move the published TokenFish folder, update Claude Code's configured command.";
    private const string ClaudeSetupManualConfigurationNote =
        "TokenFish does not edit Claude Code settings.";
    private const string ClaudeSetupWaitingGuidance =
        "If Claude remains waiting or unavailable, trigger a Claude Code status-line update and refresh TokenFish.";
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
    private readonly ProviderSelectionMode _runningProviderSelectionMode;
    private readonly CodexRuntimeMode _runningRuntimeMode;
    private readonly string _runningWslDistributionName;
    private readonly ClaudeBridgeSetup _claudeBridgeSetup;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private SavedNextLaunchSettings? _savedNextLaunchSettings;

    public CodexRuntimeSettingsPresenter(
        CodexRuntimeSettingsEditor editor,
        IProviderReadinessProvider? readinessProvider = null,
        AppSettings? runningSettings = null,
        string? applicationBaseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(editor);

        _editor = editor;
        _readinessProvider = readinessProvider ?? new ProviderReadinessProvider(null);
        _claudeBridgeSetup = ClaudeBridgeSetupGuide.Create(
            applicationBaseDirectory ?? AppContext.BaseDirectory);
        var normalizedRunningSettings = AppSettingsValidator.Normalize(
            runningSettings ?? new AppSettings());
        _runningProviderSelectionMode = normalizedRunningSettings.ProviderSelectionMode;
        _runningRuntimeMode = normalizedRunningSettings.CodexRuntimeMode;
        _runningWslDistributionName =
            normalizedRunningSettings.CodexWslDistributionName ?? string.Empty;
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
                CodexRuntimeSettingsSaveStatus.Saved => CreateSavedStateAfterSave(result),
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
        var savedProviderSelectionMode = GetSavedProviderSelectionMode(providerSelectionMode);
        var isClaudeSetupVisible = IncludesClaude(savedProviderSelectionMode);
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
            _runningProviderSelectionMode,
            savedProviderSelectionMode,
            GetProviderSelectionLabel(_runningProviderSelectionMode),
            GetProviderSelectionLabel(savedProviderSelectionMode),
            CreatePendingRestartMessage(),
            IsPendingRestartVisible(),
            isRuntimeModeEnabled,
            isWslDistributionEnabled,
            !isCodexEnabled,
            CodexSettingsRetainedDescription,
            isClaudeEnabled,
            ClaudeBridgeDescription,
            isClaudeSetupVisible,
            ClaudeSetupDescription,
            _claudeBridgeSetup.ExecutablePath,
            _claudeBridgeSetup.SettingsSnippet,
            ClaudeSetupNextSteps,
            ClaudeSetupFolderMoveNote,
            ClaudeSetupManualConfigurationNote,
            ClaudeSetupWaitingGuidance,
            _readinessProvider.CreateReadinessRows(
                _runningProviderSelectionMode,
                _runningRuntimeMode),
            !isSaving,
            isSaving,
            statusMessage,
            statusKind,
            isStatusVisible,
            focusTarget);
    }

    private CodexRuntimeSettingsViewState CreateSavedStateAfterSave(
        CodexRuntimeSettingsSaveResult result)
    {
        _savedNextLaunchSettings = new SavedNextLaunchSettings(
            result.SavedProviderSelectionMode ?? State.ProviderSelectionMode,
            result.SavedRuntimeMode ?? State.RuntimeMode,
            result.SavedWslDistributionName ?? string.Empty);

        return State with
        {
            SavedProviderSelectionMode = _savedNextLaunchSettings.Value.ProviderSelectionMode,
            SavedProviderSelectionLabel = GetProviderSelectionLabel(
                _savedNextLaunchSettings.Value.ProviderSelectionMode),
            IsClaudeSetupVisible = IncludesClaude(
                _savedNextLaunchSettings.Value.ProviderSelectionMode),
            PendingRestartMessage = CreatePendingRestartMessage(),
            IsPendingRestartVisible = IsPendingRestartVisible(),
            StatusMessage = "Settings saved.",
            StatusKind = CodexRuntimeSettingsStatusKind.Success,
            FocusTarget = CodexRuntimeSettingsFocusTarget.None,
            IsStatusVisible = true,
            CanSave = true,
            IsSaving = false
        };
    }

    private ProviderSelectionMode GetSavedProviderSelectionMode(
        ProviderSelectionMode fallbackProviderSelectionMode) =>
        _savedNextLaunchSettings?.ProviderSelectionMode ?? fallbackProviderSelectionMode;

    private bool IsPendingRestartVisible()
    {
        if (_savedNextLaunchSettings is not { } savedSettings)
        {
            return false;
        }

        return IsProviderPendingRestart(savedSettings) ||
            IsRuntimePendingRestart(savedSettings);
    }

    private string CreatePendingRestartMessage()
    {
        if (_savedNextLaunchSettings is not { } savedSettings)
        {
            return string.Empty;
        }

        var runningLabel = GetProviderSelectionLabel(_runningProviderSelectionMode);
        var savedLabel = GetProviderSelectionLabel(savedSettings.ProviderSelectionMode);
        var providerPending = IsProviderPendingRestart(savedSettings);
        var runtimePending = IsRuntimePendingRestart(savedSettings);

        if (!providerPending && !runtimePending)
        {
            return "Saved. TokenFish is already using these settings.";
        }

        if (providerPending && runtimePending)
        {
            return $"Saved. TokenFish is still using {runningLabel}.\nRestart TokenFish to use {savedLabel} with the saved runtime settings.";
        }

        if (providerPending)
        {
            return $"Saved. TokenFish is still using {runningLabel}.\nRestart TokenFish to use {savedLabel}.";
        }

        return $"Saved. TokenFish is still using {runningLabel}.\nRestart TokenFish to use the saved runtime settings.";
    }

    private bool IsProviderPendingRestart(SavedNextLaunchSettings savedSettings) =>
        savedSettings.ProviderSelectionMode != _runningProviderSelectionMode;

    private bool IsRuntimePendingRestart(SavedNextLaunchSettings savedSettings) =>
        savedSettings.RuntimeMode != _runningRuntimeMode ||
        !string.Equals(
            savedSettings.WslDistributionName,
            _runningWslDistributionName,
            StringComparison.Ordinal);

    private static bool IncludesClaude(ProviderSelectionMode providerSelectionMode) =>
        providerSelectionMode is ProviderSelectionMode.ClaudeOnly or ProviderSelectionMode.Both;

    private static string GetProviderSelectionLabel(ProviderSelectionMode providerSelectionMode) =>
        ProviderSelectionOptions
            .First(option => option.ProviderSelectionMode == providerSelectionMode)
            .Label;
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
    ProviderSelectionMode RunningProviderSelectionMode,
    ProviderSelectionMode SavedProviderSelectionMode,
    string RunningProviderSelectionLabel,
    string SavedProviderSelectionLabel,
    string PendingRestartMessage,
    bool IsPendingRestartVisible,
    bool IsRuntimeModeEnabled,
    bool IsWslDistributionEnabled,
    bool IsCodexSettingsRetainedMessageVisible,
    string CodexSettingsRetainedMessage,
    bool IsClaudeBridgeDescriptionVisible,
    string ClaudeBridgeDescription,
    bool IsClaudeSetupVisible,
    string ClaudeSetupDescription,
    string ClaudeBridgeExecutablePath,
    string ClaudeStatusLineSettingsSnippet,
    string ClaudeSetupNextSteps,
    string ClaudeSetupFolderMoveNote,
    string ClaudeSetupManualConfigurationNote,
    string ClaudeSetupWaitingGuidance,
    IReadOnlyList<ProviderReadinessDisplayState> ReadinessRows,
    bool CanSave,
    bool IsSaving,
    string StatusMessage,
    CodexRuntimeSettingsStatusKind StatusKind,
    bool IsStatusVisible,
    CodexRuntimeSettingsFocusTarget FocusTarget);

public readonly record struct SavedNextLaunchSettings(
    ProviderSelectionMode ProviderSelectionMode,
    CodexRuntimeMode RuntimeMode,
    string WslDistributionName);

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
