using TokenFish.Core.Models;
using TokenFish.Core.Settings;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class CodexRuntimeSettingsPresenterTests
{
    [Fact]
    public async Task LoadProjectsPersistedRuntimeSettings()
    {
        var presenter = CreatePresenter(
            new AppSettings
            {
                CodexRuntimeMode = CodexRuntimeMode.Wsl,
                CodexWslDistributionName = "Ubuntu-24.04"
            });

        var state = await presenter.LoadAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeMode.Wsl, state.RuntimeMode);
        Assert.Equal("Ubuntu-24.04", state.WslDistributionName);
        Assert.True(state.IsWslDistributionEnabled);
        Assert.Contains("restarted", state.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
    public async Task SuccessfulSaveReportsRestartRequirement()
    {
        var store = new RecordingSettingsStore(new AppSettings());
        var presenter = CreatePresenter(store);
        presenter.SelectRuntimeMode(CodexRuntimeMode.WslLoginShell);
        presenter.SetWslDistributionName("Ubuntu");

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.Success, state.StatusKind);
        Assert.Equal("Settings saved. Runtime changes apply after TokenFish is restarted.", state.StatusMessage);
        Assert.Equal(CodexRuntimeMode.WslLoginShell, store.SavedSettings!.CodexRuntimeMode);
        Assert.Equal("Ubuntu", store.SavedSettings.CodexWslDistributionName);
    }

    [Fact]
    public async Task ValidationFailureKeepsWindowStateEditable()
    {
        var presenter = CreatePresenter(new AppSettings());
        presenter.SelectRuntimeMode((CodexRuntimeMode)999);

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.ValidationError, state.StatusKind);
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

        var state = await presenter.SaveAsync(CancellationToken.None);

        Assert.Equal(CodexRuntimeSettingsStatusKind.PersistenceError, state.StatusKind);
        Assert.Equal("Settings could not be saved.", state.StatusMessage);
        Assert.DoesNotContain("C:\\", state.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", state.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
