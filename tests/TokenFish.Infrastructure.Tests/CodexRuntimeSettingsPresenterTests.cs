using TokenFish.Core.Models;
using TokenFish.Core.Settings;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class CodexRuntimeSettingsPresenterTests
{
    [Fact]
    public void InitialStateHasNoVisibleStatusMessage()
    {
        var presenter = CreatePresenter(new AppSettings());

        Assert.False(presenter.State.IsStatusVisible);
        Assert.Equal(string.Empty, presenter.State.StatusMessage);
    }

    [Fact]
    public async Task LoadProjectsPersistedRuntimeSettings()
    {
        var presenter = CreatePresenter(
            new AppSettings
            {
                ProviderSelectionMode = ProviderSelectionMode.Both,
                CodexRuntimeMode = CodexRuntimeMode.Wsl,
                CodexWslDistributionName = "Ubuntu-24.04"
            });

        var state = await presenter.LoadAsync(CancellationToken.None);

        Assert.Equal(ProviderSelectionMode.Both, state.ProviderSelectionMode);
        Assert.Equal(CodexRuntimeMode.Wsl, state.RuntimeMode);
        Assert.Equal("Ubuntu-24.04", state.WslDistributionName);
        Assert.True(state.IsRuntimeModeEnabled);
        Assert.True(state.IsWslDistributionEnabled);
        Assert.False(state.IsStatusVisible);
        Assert.Equal(string.Empty, state.StatusMessage);
    }

    [Fact]
    public void RuntimeModeLabelsAreUserFacing()
    {
        var presenter = CreatePresenter(new AppSettings());

        var labels = presenter.State.RuntimeModeOptions
            .Select(option => option.Label)
            .ToArray();

        Assert.Equal(["WSL login shell", "WSL direct", "Native Windows"], labels);
        Assert.DoesNotContain("WslLoginShell", labels);
    }

    [Fact]
    public void ProviderModeLabelsAreUserFacing()
    {
        var presenter = CreatePresenter(new AppSettings());

        var labels = presenter.State.ProviderSelectionOptions
            .Select(option => option.Label)
            .ToArray();

        Assert.Equal(["Codex", "Claude", "Codex and Claude"], labels);
        Assert.DoesNotContain("CodexOnly", labels);
        Assert.DoesNotContain("ClaudeOnly", labels);
    }

    [Theory]
    [InlineData(ProviderSelectionMode.CodexOnly, true, false)]
    [InlineData(ProviderSelectionMode.ClaudeOnly, false, true)]
    [InlineData(ProviderSelectionMode.Both, true, true)]
    public async Task ProviderSelectionUpdatesContextualControlState(
        ProviderSelectionMode providerSelectionMode,
        bool codexEnabled,
        bool claudeEnabled)
    {
        var presenter = CreatePresenter(
            new AppSettings
            {
                CodexRuntimeMode = CodexRuntimeMode.Wsl,
                CodexWslDistributionName = "Ubuntu"
            });
        await presenter.LoadAsync(CancellationToken.None);

        var state = presenter.SelectProviderSelectionMode(providerSelectionMode);

        Assert.Equal(providerSelectionMode, state.ProviderSelectionMode);
        Assert.Equal(codexEnabled, state.IsRuntimeModeEnabled);
        Assert.Equal(codexEnabled, state.IsWslDistributionEnabled);
        Assert.Equal(!codexEnabled, state.IsCodexSettingsRetainedMessageVisible);
        Assert.Equal(claudeEnabled, state.IsClaudeBridgeDescriptionVisible);
        Assert.Equal("Ubuntu", state.WslDistributionName);
    }

    [Fact]
    public void SelectingNativeWindowsDisablesAndClearsWslDistribution()
    {
        var presenter = CreatePresenter(
            new AppSettings
            {
                CodexRuntimeMode = CodexRuntimeMode.Wsl,
                CodexWslDistributionName = "Ubuntu"
            });
        presenter.SetWslDistributionName("Ubuntu");

        var state = presenter.SelectRuntimeMode(CodexRuntimeMode.NativeWindows);

        Assert.Equal(CodexRuntimeMode.NativeWindows, state.RuntimeMode);
        Assert.False(state.IsWslDistributionEnabled);
        Assert.Equal(string.Empty, state.WslDistributionName);
    }

    [Fact]
    public async Task SelectingClaudeOnlyDoesNotClearRuntimeValuesBeforeSave()
    {
        var presenter = CreatePresenter(
            new AppSettings
            {
                CodexRuntimeMode = CodexRuntimeMode.Wsl,
                CodexWslDistributionName = "Ubuntu"
            });
        await presenter.LoadAsync(CancellationToken.None);

        var state = presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);

        Assert.False(state.IsRuntimeModeEnabled);
        Assert.False(state.IsWslDistributionEnabled);
        Assert.Equal(CodexRuntimeMode.Wsl, state.RuntimeMode);
        Assert.Equal("Ubuntu", state.WslDistributionName);
    }

    [Fact]
    public void SwitchingProviderOptionsBeforeSaveDoesNotPersistSettings()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var presenter = CreatePresenter(store);

        presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);

        Assert.Equal(0, store.SaveCallCount);
        Assert.Null(store.SavedSettings);
    }

    [Fact]
    public async Task SuccessfulSaveReportsRestartRequirement()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var presenter = CreatePresenter(store);
        presenter.SelectRuntimeMode(CodexRuntimeMode.WslLoginShell);
        presenter.SetWslDistributionName("Ubuntu");

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.Success, state.StatusKind);
        Assert.True(state.IsStatusVisible);
        Assert.Equal("Runtime changes take effect after TokenFish restarts.", state.StatusMessage);
        Assert.Equal(CodexRuntimeMode.WslLoginShell, store.SavedSettings!.CodexRuntimeMode);
        Assert.Equal("Ubuntu", store.SavedSettings.CodexWslDistributionName);
    }

    [Fact]
    public async Task ProviderOnlySaveReportsProviderRestartRequirement()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var presenter = CreatePresenter(store);
        presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.Success, state.StatusKind);
        Assert.Equal("Provider changes take effect after TokenFish restarts.", state.StatusMessage);
        Assert.Equal(ProviderSelectionMode.ClaudeOnly, store.SavedSettings!.ProviderSelectionMode);
    }

    [Fact]
    public async Task ProviderAndRuntimeSaveReportsCombinedRestartRequirement()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var presenter = CreatePresenter(store);
        presenter.SelectProviderSelectionMode(ProviderSelectionMode.Both);
        presenter.SelectRuntimeMode(CodexRuntimeMode.Wsl);
        presenter.SetWslDistributionName("Ubuntu");

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.Success, state.StatusKind);
        Assert.Equal(
            "Provider and runtime changes take effect after TokenFish restarts.",
            state.StatusMessage);
        Assert.Equal(ProviderSelectionMode.Both, store.SavedSettings!.ProviderSelectionMode);
        Assert.Equal(CodexRuntimeMode.Wsl, store.SavedSettings.CodexRuntimeMode);
        Assert.Equal("Ubuntu", store.SavedSettings.CodexWslDistributionName);
    }

    [Fact]
    public async Task UnchangedSaveDoesNotReportRestartRequirement()
    {
        var presenter = CreatePresenter(new AppSettings());

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.Information, state.StatusKind);
        Assert.True(state.IsStatusVisible);
        Assert.Equal("Settings are already up to date.", state.StatusMessage);
    }

    [Fact]
    public async Task ValidationFailureKeepsWindowStateEditable()
    {
        var presenter = CreatePresenter(new AppSettings());
        presenter.SelectRuntimeMode((CodexRuntimeMode)999);

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.ValidationError, state.StatusKind);
        Assert.True(state.IsStatusVisible);
        Assert.Equal("Settings are not valid.", state.StatusMessage);
        Assert.Equal(CodexRuntimeSettingsFocusTarget.RuntimeMode, state.FocusTarget);
        Assert.True(state.CanSave);
    }

    [Fact]
    public async Task PersistenceFailureUsesGenericMessage()
    {
        var store = new RecordingSettingsStore(new AppSettings())
        {
            SaveException = new InvalidOperationException("C:\\Users\\pixel\\secret\\settings.json")
        };
        var presenter = CreatePresenter(store);
        presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.PersistenceError, state.StatusKind);
        Assert.True(state.IsStatusVisible);
        Assert.Equal("Settings could not be saved.", state.StatusMessage);
        Assert.DoesNotContain("C:\\", state.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", state.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReloadReturnsToNeutralStatusState()
    {
        var presenter = CreatePresenter(new AppSettings());
        await presenter.SaveAsync(CancellationToken.None);

        var state = await presenter.LoadAsync(CancellationToken.None);

        Assert.False(state.IsStatusVisible);
        Assert.Equal(string.Empty, state.StatusMessage);
    }

    [Fact]
    public async Task DuplicateSaveSubmissionIsIgnoredWhileSaveIsActive()
    {
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new RecordingSettingsStore(new AppSettings())
        {
            BeforeSaveAsync = async () =>
            {
                saveEntered.SetResult();
                await saveRelease.Task;
            }
        };
        var presenter = CreatePresenter(store);
        presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);

        var firstSave = presenter.SaveAsync(CancellationToken.None);
        await saveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var duplicateState = await presenter.SaveAsync(CancellationToken.None);
        saveRelease.SetResult();
        await firstSave;

        Assert.True(duplicateState.IsSaving);
        Assert.False(duplicateState.CanSave);
        Assert.Equal(1, store.SaveCallCount);
    }

    private static CodexRuntimeSettingsPresenter CreatePresenter(AppSettings settings) =>
        CreatePresenter(new RecordingSettingsStore(settings));

    private static CodexRuntimeSettingsPresenter CreatePresenter(RecordingSettingsStore store) =>
        new(new CodexRuntimeSettingsEditor(store));

    private sealed class RecordingSettingsStore : IAppSettingsStore
    {
        private readonly AppSettings _settings;

        public RecordingSettingsStore(AppSettings settings)
        {
            _settings = settings;
        }

        public int SaveCallCount { get; private set; }

        public AppSettings? SavedSettings { get; private set; }

        public Exception? SaveException { get; init; }

        public Func<Task>? BeforeSaveAsync { get; init; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_settings);
        }

        public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;

            if (BeforeSaveAsync is not null)
            {
                await BeforeSaveAsync();
            }

            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedSettings = settings;
        }
    }
}
