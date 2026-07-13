using TokenFish.Core.Models;
using TokenFish.Core.Settings;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class CodexRuntimeSettingsEditorTests
{
    [Fact]
    public async Task LoadDraftUsesCurrentPersistedRuntimeSettings()
    {
        var store = new RecordingSettingsStore(
            new AppSettings
            {
                ProviderSelectionMode = ProviderSelectionMode.Both,
                CodexRuntimeMode = CodexRuntimeMode.Wsl,
                CodexWslDistributionName = "Ubuntu-24.04"
            });
        var editor = new CodexRuntimeSettingsEditor(store);

        var draft = await editor.LoadDraftAsync(CancellationToken.None);

        Assert.Equal(ProviderSelectionMode.Both, draft.ProviderSelectionMode);
        Assert.Equal(CodexRuntimeMode.Wsl, draft.RuntimeMode);
        Assert.Equal("Ubuntu-24.04", draft.WslDistributionName);
    }

    [Fact]
    public async Task ExistingUneditedSettingsArePreserved()
    {
        var store = new RecordingSettingsStore(
            new AppSettings
            {
                ProviderSelectionMode = ProviderSelectionMode.Both,
                ThemeMode = ThemeMode.Arcade,
                CodexRuntimeMode = CodexRuntimeMode.WslLoginShell
            });
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(ProviderSelectionMode.Both, CodexRuntimeMode.Wsl, "Ubuntu"),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.Saved, result.Status);
        Assert.Equal(ProviderSelectionMode.Both, store.SavedSettings!.ProviderSelectionMode);
        Assert.Equal(ThemeMode.Arcade, store.SavedSettings.ThemeMode);
        Assert.Equal(CodexRuntimeMode.Wsl, store.SavedSettings.CodexRuntimeMode);
        Assert.Equal("Ubuntu", store.SavedSettings.CodexWslDistributionName);
    }

    [Fact]
    public async Task ValidWslLoginShellConfigurationSaves()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(
                ProviderSelectionMode.CodexOnly,
                CodexRuntimeMode.WslLoginShell,
                "Ubuntu-24.04"),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.Saved, result.Status);
        Assert.Equal(ProviderSelectionMode.CodexOnly, store.SavedSettings!.ProviderSelectionMode);
        Assert.Equal(CodexRuntimeMode.WslLoginShell, store.SavedSettings!.CodexRuntimeMode);
        Assert.Equal("Ubuntu-24.04", store.SavedSettings.CodexWslDistributionName);
    }

    [Fact]
    public async Task ValidDirectWslConfigurationSaves()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(ProviderSelectionMode.ClaudeOnly, CodexRuntimeMode.Wsl, "Ubuntu"),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.Saved, result.Status);
        Assert.Equal(ProviderSelectionMode.ClaudeOnly, store.SavedSettings!.ProviderSelectionMode);
        Assert.Equal(CodexRuntimeMode.Wsl, store.SavedSettings!.CodexRuntimeMode);
        Assert.Equal("Ubuntu", store.SavedSettings.CodexWslDistributionName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ValidNativeConfigurationSavesWhenDistributionIsEmpty(
        string? distributionName)
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(
                ProviderSelectionMode.Both,
                CodexRuntimeMode.NativeWindows,
                distributionName),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.Saved, result.Status);
        Assert.Equal(ProviderSelectionMode.Both, store.SavedSettings!.ProviderSelectionMode);
        Assert.Equal(CodexRuntimeMode.NativeWindows, store.SavedSettings!.CodexRuntimeMode);
        Assert.Null(store.SavedSettings.CodexWslDistributionName);
    }

    [Fact]
    public async Task NativeModeWithDistributionIsRejected()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(
                ProviderSelectionMode.CodexOnly,
                CodexRuntimeMode.NativeWindows,
                "Ubuntu"),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.ValidationFailed, result.Status);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task WhitespaceDistributionNormalizesToNull()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(ProviderSelectionMode.CodexOnly, CodexRuntimeMode.Wsl, "   "),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.Saved, result.Status);
        Assert.Null(store.SavedSettings!.CodexWslDistributionName);
    }

    [Fact]
    public async Task InvalidModeIsRejected()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(ProviderSelectionMode.CodexOnly, (CodexRuntimeMode)999, null),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.ValidationFailed, result.Status);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task StoreFailureMapsToSafeGenericResult()
    {
        var store = new RecordingSettingsStore(new AppSettings())
        {
            SaveException = new InvalidOperationException("C:\\Users\\pixel\\secret\\settings.json")
        };
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(ProviderSelectionMode.CodexOnly, CodexRuntimeMode.Wsl, "Ubuntu"),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.PersistenceFailed, result.Status);
        Assert.Equal("Settings could not be saved.", result.Message);
        Assert.DoesNotContain("C:\\", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExistingProviderModeSurvivesRuntimeSettingEdits()
    {
        var store = new RecordingSettingsStore(
            new AppSettings
            {
                ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly,
                CodexRuntimeMode = CodexRuntimeMode.WslLoginShell
            });
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(
                ProviderSelectionMode.ClaudeOnly,
                CodexRuntimeMode.Wsl,
                "Ubuntu"),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.Saved, result.Status);
        Assert.Equal(ProviderSelectionMode.ClaudeOnly, store.SavedSettings!.ProviderSelectionMode);
        Assert.Equal(CodexRuntimeMode.Wsl, store.SavedSettings.CodexRuntimeMode);
        Assert.Equal("Ubuntu", store.SavedSettings.CodexWslDistributionName);
    }

    [Fact]
    public async Task UnchangedDraftDoesNotWriteSettings()
    {
        var store = new RecordingSettingsStore(
            new AppSettings
            {
                ProviderSelectionMode = ProviderSelectionMode.CodexOnly,
                CodexRuntimeMode = CodexRuntimeMode.Wsl,
                CodexWslDistributionName = "Ubuntu"
            });
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(
                ProviderSelectionMode.CodexOnly,
                CodexRuntimeMode.Wsl,
                "Ubuntu"),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.Unchanged, result.Status);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task InvalidProviderModeIsRejected()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var editor = new CodexRuntimeSettingsEditor(store);

        var result = await editor.SaveAsync(
            new CodexRuntimeSettingsDraft(
                (ProviderSelectionMode)999,
                CodexRuntimeMode.WslLoginShell,
                null),
            CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsSaveStatus.ValidationFailed, result.Status);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public void EditingWorkflowDependsOnlyOnSettingsStore()
    {
        var constructor = Assert.Single(typeof(CodexRuntimeSettingsEditor).GetConstructors());

        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(IAppSettingsStore), parameter.ParameterType);
    }

    private sealed class RecordingSettingsStore : IAppSettingsStore
    {
        private readonly AppSettings _settings;

        public RecordingSettingsStore(AppSettings settings)
        {
            _settings = settings;
        }

        public int LoadCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public AppSettings? SavedSettings { get; private set; }

        public Exception? SaveException { get; init; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCallCount++;
            return Task.FromResult(_settings);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;

            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedSettings = settings;
            return Task.CompletedTask;
        }
    }
}
