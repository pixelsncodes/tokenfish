using TokenFish.Core.Models;
using TokenFish.Core.Settings;

namespace TokenFish.Infrastructure;

public sealed class CodexRuntimeSettingsEditor
{
    private readonly IAppSettingsStore _settingsStore;

    public CodexRuntimeSettingsEditor(IAppSettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);

        _settingsStore = settingsStore;
    }

    public async Task<CodexRuntimeSettingsDraft> LoadDraftAsync(
        CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        var normalizedSettings = AppSettingsValidator.Normalize(settings);

        return new CodexRuntimeSettingsDraft(
            normalizedSettings.ProviderSelectionMode,
            normalizedSettings.CodexRuntimeMode,
            normalizedSettings.CodexWslDistributionName);
    }

    public async Task<CodexRuntimeSettingsSaveResult> SaveAsync(
        CodexRuntimeSettingsDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        AppSettings currentSettings;
        try
        {
            currentSettings = await _settingsStore.LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return CodexRuntimeSettingsSaveResult.PersistenceFailed;
        }

        AppSettings normalizedSettings;
        try
        {
            currentSettings = AppSettingsValidator.Normalize(currentSettings);
            normalizedSettings = AppSettingsValidator.Normalize(
                currentSettings with
                {
                    ProviderSelectionMode = draft.ProviderSelectionMode,
                    CodexRuntimeMode = draft.RuntimeMode,
                    CodexWslDistributionName = draft.WslDistributionName
                });
        }
        catch (ArgumentException)
        {
            return CodexRuntimeSettingsSaveResult.ValidationFailed;
        }

        var providerChanged =
            currentSettings.ProviderSelectionMode != normalizedSettings.ProviderSelectionMode;
        var runtimeChanged =
            currentSettings.CodexRuntimeMode != normalizedSettings.CodexRuntimeMode ||
            currentSettings.CodexWslDistributionName != normalizedSettings.CodexWslDistributionName;

        if (!providerChanged && !runtimeChanged)
        {
            return CodexRuntimeSettingsSaveResult.Unchanged;
        }

        try
        {
            await _settingsStore.SaveAsync(normalizedSettings, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return CodexRuntimeSettingsSaveResult.PersistenceFailed;
        }

        return CodexRuntimeSettingsSaveResult.Saved(
            providerChanged,
            runtimeChanged,
            normalizedSettings.ProviderSelectionMode,
            normalizedSettings.CodexRuntimeMode,
            normalizedSettings.CodexWslDistributionName);
    }
}

public sealed record CodexRuntimeSettingsDraft(
    ProviderSelectionMode ProviderSelectionMode,
    CodexRuntimeMode RuntimeMode,
    string? WslDistributionName);

public sealed record CodexRuntimeSettingsSaveResult(
    CodexRuntimeSettingsSaveStatus Status,
    string Message,
    bool ProviderChanged = false,
    bool RuntimeChanged = false,
    ProviderSelectionMode? SavedProviderSelectionMode = null,
    CodexRuntimeMode? SavedRuntimeMode = null,
    string? SavedWslDistributionName = null)
{
    public static CodexRuntimeSettingsSaveResult Saved(
        bool providerChanged,
        bool runtimeChanged,
        ProviderSelectionMode savedProviderSelectionMode,
        CodexRuntimeMode savedRuntimeMode,
        string? savedWslDistributionName) =>
        new(
            CodexRuntimeSettingsSaveStatus.Saved,
            "Settings saved.",
            providerChanged,
            runtimeChanged,
            savedProviderSelectionMode,
            savedRuntimeMode,
            savedWslDistributionName);

    public static CodexRuntimeSettingsSaveResult Unchanged { get; } =
        new(CodexRuntimeSettingsSaveStatus.Unchanged, "Settings are already up to date.");

    public static CodexRuntimeSettingsSaveResult ValidationFailed { get; } =
        new(CodexRuntimeSettingsSaveStatus.ValidationFailed, "Settings are not valid.");

    public static CodexRuntimeSettingsSaveResult PersistenceFailed { get; } =
        new(CodexRuntimeSettingsSaveStatus.PersistenceFailed, "Settings could not be saved.");
}

public enum CodexRuntimeSettingsSaveStatus
{
    Unchanged,
    Saved,
    ValidationFailed,
    PersistenceFailed
}
