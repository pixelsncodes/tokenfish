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
            normalizedSettings = AppSettingsValidator.Normalize(
                currentSettings with
                {
                    CodexRuntimeMode = draft.RuntimeMode,
                    CodexWslDistributionName = draft.WslDistributionName
                });
        }
        catch (ArgumentException)
        {
            return CodexRuntimeSettingsSaveResult.ValidationFailed;
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

        return CodexRuntimeSettingsSaveResult.Saved;
    }
}

public sealed record CodexRuntimeSettingsDraft(
    CodexRuntimeMode RuntimeMode,
    string? WslDistributionName);

public sealed record CodexRuntimeSettingsSaveResult(
    CodexRuntimeSettingsSaveStatus Status,
    string Message)
{
    public static CodexRuntimeSettingsSaveResult Saved { get; } =
        new(CodexRuntimeSettingsSaveStatus.Saved, "Settings saved.");

    public static CodexRuntimeSettingsSaveResult ValidationFailed { get; } =
        new(CodexRuntimeSettingsSaveStatus.ValidationFailed, "Settings are not valid.");

    public static CodexRuntimeSettingsSaveResult PersistenceFailed { get; } =
        new(CodexRuntimeSettingsSaveStatus.PersistenceFailed, "Settings could not be saved.");
}

public enum CodexRuntimeSettingsSaveStatus
{
    Saved,
    ValidationFailed,
    PersistenceFailed
}
