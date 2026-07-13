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
    public async Task EditableProviderSelectionDoesNotChangeCurrentReadinessRows()
    {
        var readinessProvider = new RecordingReadinessProvider();
        var store = new RecordingSettingsStore(new AppSettings());
        var presenter = CreatePresenter(store, readinessProvider);
        await presenter.LoadAsync(CancellationToken.None);

        var state = presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);

        Assert.Equal([ProviderKind.Codex], state.ReadinessRows.Select(row => row.Provider));
        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(3, readinessProvider.CallCount);
    }

    [Fact]
    public async Task RunningCombinedModeShowsBothReadinessRows()
    {
        var presenter = CreatePresenter(
            new RecordingSettingsStore(new AppSettings()),
            runningSettings: new AppSettings
            {
                ProviderSelectionMode = ProviderSelectionMode.Both
            });
        await presenter.LoadAsync(CancellationToken.None);

        var state = presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);

        Assert.Equal([ProviderKind.Codex, ProviderKind.Claude], state.ReadinessRows.Select(row => row.Provider));
    }

    [Fact]
    public async Task ReadinessRowsContainNoRawExceptionTextOrPaths()
    {
        var readinessProvider = new FixedReadinessProvider(
        [
            new ProviderReadinessDisplayState(
                ProviderKind.Claude,
                "Claude",
                ProviderReadinessKind.Unavailable,
                "Unavailable",
                "Claude usage is unavailable.")
        ]);
        var presenter = CreatePresenter(
            new RecordingSettingsStore(
                new AppSettings { ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly }),
            readinessProvider);

        var state = await presenter.LoadAsync(CancellationToken.None);
        var row = Assert.Single(state.ReadinessRows);

        Assert.DoesNotContain("Exception", row.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", row.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/mnt/", row.Description, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal("Settings saved.", state.StatusMessage);
        Assert.True(state.IsPendingRestartVisible);
        Assert.Equal(
            "Saved. TokenFish is still using Codex.\nRestart TokenFish to use the saved runtime settings.",
            state.PendingRestartMessage);
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
        Assert.Equal("Settings saved.", state.StatusMessage);
        Assert.True(state.IsPendingRestartVisible);
        Assert.Equal(ProviderSelectionMode.CodexOnly, state.RunningProviderSelectionMode);
        Assert.Equal(ProviderSelectionMode.ClaudeOnly, state.SavedProviderSelectionMode);
        Assert.Equal("Codex", state.RunningProviderSelectionLabel);
        Assert.Equal("Claude", state.SavedProviderSelectionLabel);
        Assert.Equal(
            "Saved. TokenFish is still using Codex.\nRestart TokenFish to use Claude.",
            state.PendingRestartMessage);
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
        Assert.Equal("Settings saved.", state.StatusMessage);
        Assert.True(state.IsPendingRestartVisible);
        Assert.Equal(
            "Saved. TokenFish is still using Codex.\nRestart TokenFish to use Codex and Claude with the saved runtime settings.",
            state.PendingRestartMessage);
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
        Assert.False(state.IsPendingRestartVisible);
        Assert.Equal(string.Empty, state.PendingRestartMessage);
    }

    [Fact]
    public async Task SavedProviderModeDoesNotAlterRunningProviderMode()
    {
        var presenter = CreatePresenter(new AppSettings());
        presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(ProviderSelectionMode.CodexOnly, state.RunningProviderSelectionMode);
        Assert.Equal(ProviderSelectionMode.ClaudeOnly, state.ProviderSelectionMode);
        Assert.Equal(ProviderSelectionMode.ClaudeOnly, state.SavedProviderSelectionMode);
    }

    [Fact]
    public async Task ReturningSelectionToRunningModeClearsPendingRestartAfterSave()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var presenter = CreatePresenter(store);
        presenter.SelectProviderSelectionMode(ProviderSelectionMode.ClaudeOnly);
        var pendingState = await presenter.SaveAsync(CancellationToken.None);

        presenter.SelectProviderSelectionMode(ProviderSelectionMode.CodexOnly);
        var clearedState = await presenter.SaveAsync(CancellationToken.None);

        Assert.True(pendingState.IsPendingRestartVisible);
        Assert.False(clearedState.IsPendingRestartVisible);
        Assert.Equal("Saved. TokenFish is already using these settings.", clearedState.PendingRestartMessage);
        Assert.Equal(ProviderSelectionMode.CodexOnly, store.SavedSettings!.ProviderSelectionMode);
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
        Assert.False(state.IsPendingRestartVisible);
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

    private static CodexRuntimeSettingsPresenter CreatePresenter(
        RecordingSettingsStore store,
        IProviderReadinessProvider? readinessProvider = null,
        AppSettings? runningSettings = null) =>
        new(new CodexRuntimeSettingsEditor(store), readinessProvider, runningSettings);

    private sealed class RecordingReadinessProvider : IProviderReadinessProvider
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<ProviderReadinessDisplayState> CreateReadinessRows(
            ProviderSelectionMode providerSelectionMode,
            CodexRuntimeMode codexRuntimeMode)
        {
            _ = codexRuntimeMode;
            CallCount++;
            return providerSelectionMode switch
            {
                ProviderSelectionMode.CodexOnly => [CreateRow(ProviderKind.Codex)],
                ProviderSelectionMode.ClaudeOnly => [CreateRow(ProviderKind.Claude)],
                ProviderSelectionMode.Both => [CreateRow(ProviderKind.Codex), CreateRow(ProviderKind.Claude)],
                _ => []
            };
        }
    }

    private sealed class FixedReadinessProvider(
        IReadOnlyList<ProviderReadinessDisplayState> rows) : IProviderReadinessProvider
    {
        public IReadOnlyList<ProviderReadinessDisplayState> CreateReadinessRows(
            ProviderSelectionMode providerSelectionMode,
            CodexRuntimeMode codexRuntimeMode)
        {
            _ = providerSelectionMode;
            _ = codexRuntimeMode;
            return rows;
        }
    }

    private static ProviderReadinessDisplayState CreateRow(ProviderKind provider) =>
        new(
            provider,
            provider.ToString(),
            ProviderReadinessKind.WaitingForData,
            "Waiting for data",
            "Waiting for normalized usage.");

    private sealed class RecordingSettingsStore : IAppSettingsStore
    {
        private AppSettings _settings;

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
            _settings = settings;
        }
    }
}
