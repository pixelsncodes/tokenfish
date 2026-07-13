using TokenFish.Infrastructure;
using TokenFish.Core.Providers;

namespace TokenFish.Infrastructure.Tests;

public sealed class NotificationAreaControllerTests
{
    [Fact]
    public void IconInitializationIsRequestedOnce()
    {
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(icon: icon);

        controller.Initialize();
        controller.Initialize();

        Assert.Equal(1, icon.InitializeCallCount);
    }

    [Fact]
    public void ExplorerRestartRequestsReadditionWithoutDuplicateInitialization()
    {
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(icon: icon);

        controller.Initialize();
        controller.RestoreIcon();
        controller.RestoreIcon();

        Assert.Equal(1, icon.InitializeCallCount);
        Assert.Equal(2, icon.RestoreCallCount);
    }

    [Fact]
    public async Task PrimaryActivationRaisesToggleCommand()
    {
        var toggleCount = 0;
        var toggleSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(
            icon: icon,
            toggleWindowAsync: () =>
            {
                toggleCount++;
                toggleSignal.SetResult();
                return Task.CompletedTask;
            });
        controller.Initialize();

        icon.RaiseCommand(NotificationAreaCommand.PrimaryActivate);
        await toggleSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, toggleCount);
    }

    [Fact]
    public async Task RefreshDispatchesOneHostRefreshRequest()
    {
        var host = new RecordingRuntimeHost();
        var refreshEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.RefreshAsyncCallback = async _ =>
        {
            refreshEntered.SetResult();
            await refreshRelease.Task;
        };
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(icon: icon, host: host);
        controller.Initialize();

        icon.RaiseCommand(NotificationAreaCommand.Refresh);
        await refreshEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        icon.RaiseCommand(NotificationAreaCommand.Refresh);
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        refreshRelease.SetResult();
        await host.WaitForRefreshCountAsync(1);

        Assert.Equal(1, host.RefreshCallCount);
    }

    [Fact]
    public async Task RuntimeActiveRefreshDisablesManualRefreshCommand()
    {
        var host = new RecordingRuntimeHost();
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(icon: icon, host: host);
        controller.Initialize();

        host.SetRefreshStatus(new ProviderRefreshStatus(
            IsRefreshActive: true,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: null,
            ProviderRefreshOutcome.Refreshing,
            Version: 1));
        icon.RaiseCommand(NotificationAreaCommand.Refresh);
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, host.RefreshCallCount);
        Assert.Equal(NotificationAreaRefreshCommandState.Updating, icon.RefreshCommandState);

        host.SetRefreshStatus(new ProviderRefreshStatus(
            IsRefreshActive: false,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            ProviderRefreshOutcome.Succeeded,
            Version: 2));

        Assert.Equal(NotificationAreaRefreshCommandState.Available, icon.RefreshCommandState);
    }

    [Fact]
    public async Task ManualRefreshDisablesCommandUntilCompletion()
    {
        var host = new RecordingRuntimeHost();
        var refreshEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.RefreshAsyncCallback = async _ =>
        {
            refreshEntered.SetResult();
            await refreshRelease.Task;
        };
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(icon: icon, host: host);
        controller.Initialize();

        icon.RaiseCommand(NotificationAreaCommand.Refresh);
        await refreshEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(NotificationAreaRefreshCommandState.Updating, icon.RefreshCommandState);

        refreshRelease.SetResult();
        await host.WaitForRefreshCountAsync(1);
        await WaitForRefreshCommandStateAsync(icon, NotificationAreaRefreshCommandState.Available);

        Assert.Equal(NotificationAreaRefreshCommandState.Available, icon.RefreshCommandState);
    }

    [Fact]
    public async Task ManualRefreshCommandIsRestoredAfterExpectedFailure()
    {
        var host = new RecordingRuntimeHost
        {
            RefreshAsyncCallback = _ => Task.FromException(
                new InvalidOperationException("synthetic provider failure"))
        };
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(icon: icon, host: host);
        controller.Initialize();

        icon.RaiseCommand(NotificationAreaCommand.Refresh);
        await host.WaitForRefreshCountAsync(1);
        await WaitForRefreshCommandStateAsync(icon, NotificationAreaRefreshCommandState.Available);

        Assert.Equal(1, host.RefreshCallCount);
        Assert.Equal(0, host.ShellFaultCallCount);
        Assert.Equal(NotificationAreaRefreshCommandState.Available, icon.RefreshCommandState);
    }

    [Fact]
    public async Task SettingsDispatchesOpenWithoutRefreshingRuntime()
    {
        var settingsOpenCount = 0;
        var settingsSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new RecordingRuntimeHost();
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(
            icon: icon,
            host: host,
            openSettingsAsync: () =>
            {
                settingsOpenCount++;
                settingsSignal.SetResult();
                return Task.CompletedTask;
            });
        controller.Initialize();

        icon.RaiseCommand(NotificationAreaCommand.Settings);
        await settingsSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, settingsOpenCount);
        Assert.Equal(0, host.RefreshCallCount);
    }

    [Fact]
    public async Task ExitDispatchesOneShutdownRequest()
    {
        var exitCount = 0;
        var exitSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(
            icon: icon,
            exitAsync: () =>
            {
                exitCount++;
                exitSignal.SetResult();
                return Task.CompletedTask;
            });
        controller.Initialize();

        icon.RaiseCommand(NotificationAreaCommand.Exit);
        await exitSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        icon.RaiseCommand(NotificationAreaCommand.Exit);
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, exitCount);
    }

    [Fact]
    public async Task CommandsAreIgnoredAfterShutdownStarts()
    {
        var toggleCount = 0;
        var exitRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(
            icon: icon,
            toggleWindowAsync: () =>
            {
                toggleCount++;
                return Task.CompletedTask;
            },
            exitAsync: () => exitRelease.Task);
        controller.Initialize();

        icon.RaiseCommand(NotificationAreaCommand.Exit);
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        icon.RaiseCommand(NotificationAreaCommand.PrimaryActivate);
        icon.RaiseCommand(NotificationAreaCommand.Refresh);
        exitRelease.SetResult();
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, toggleCount);
    }

    [Fact]
    public async Task RefreshStatusCompletionAfterDisposalDoesNotReenableCommand()
    {
        var host = new RecordingRuntimeHost();
        var icon = new RecordingNotificationAreaIcon();
        var controller = CreateController(icon: icon, host: host);
        controller.Initialize();
        host.SetRefreshStatus(new ProviderRefreshStatus(
            IsRefreshActive: true,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: null,
            ProviderRefreshOutcome.Refreshing,
            Version: 1));

        controller.Dispose();
        host.SetRefreshStatus(new ProviderRefreshStatus(
            IsRefreshActive: false,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero),
            ProviderRefreshOutcome.Succeeded,
            Version: 2));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        Assert.Equal(NotificationAreaRefreshCommandState.Updating, icon.RefreshCommandState);
    }

    [Fact]
    public void RepeatedDisposalIsSafe()
    {
        var icon = new RecordingNotificationAreaIcon();
        var controller = CreateController(icon: icon);

        controller.Initialize();
        controller.Dispose();
        controller.Dispose();

        Assert.Equal(1, icon.DisposeCallCount);
    }

    [Fact]
    public void NativeFailureMapsToGenericShellFaultWithoutExceptionText()
    {
        var host = new RecordingRuntimeHost();
        var icon = new RecordingNotificationAreaIcon();
        using var controller = CreateController(icon: icon, host: host);
        controller.Initialize();

        icon.RaiseShellFaulted();

        Assert.Equal(1, host.ShellFaultCallCount);
        Assert.Equal(ApplicationRuntimeIssue.ShellFailed, host.Status.Issue);
        Assert.DoesNotContain("exception", host.Status.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static NotificationAreaController CreateController(
        RecordingNotificationAreaIcon? icon = null,
        RecordingRuntimeHost? host = null,
        Func<Task>? toggleWindowAsync = null,
        Func<Task>? openSettingsAsync = null,
        Func<Task>? exitAsync = null)
    {
        var runtimeHost = host ?? new RecordingRuntimeHost();
        return new NotificationAreaController(
            icon ?? new RecordingNotificationAreaIcon(),
            runtimeHost,
            new ManualRefreshCommand(runtimeHost),
            toggleWindowAsync ?? (() => Task.CompletedTask),
            openSettingsAsync ?? (() => Task.CompletedTask),
            exitAsync ?? (() => Task.CompletedTask));
    }

    private static async Task WaitForRefreshCommandStateAsync(
        RecordingNotificationAreaIcon icon,
        NotificationAreaRefreshCommandState state)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (icon.RefreshCommandState != state)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    private sealed class RecordingNotificationAreaIcon : INotificationAreaIcon
    {
        public event Action<NotificationAreaCommand>? CommandRequested;

        public event Action? ShellFaulted;

        public int InitializeCallCount { get; private set; }

        public int RestoreCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public NotificationAreaRefreshCommandState RefreshCommandState { get; private set; } =
            NotificationAreaRefreshCommandState.Available;

        public void Initialize() => InitializeCallCount++;

        public void Restore() => RestoreCallCount++;

        public void Dispose() => DisposeCallCount++;

        public void SetRefreshCommandState(NotificationAreaRefreshCommandState state) =>
            RefreshCommandState = state;

        public void RaiseCommand(NotificationAreaCommand command) =>
            CommandRequested?.Invoke(command);

        public void RaiseShellFaulted() => ShellFaulted?.Invoke();
    }

    private sealed class RecordingRuntimeHost : IApplicationRuntimeHost
    {
        private readonly SemaphoreSlim _refreshSignal = new(0);

        public ApplicationRuntimeStatus Status { get; private set; } =
            ApplicationRuntimeStatus.Running;

        public ProviderRefreshStatus RefreshStatus { get; private set; } =
            ProviderRefreshStatus.Initial;

        public TokenFish.Core.Models.AppSettings? CurrentSettings { get; }

        public event Action<ApplicationRuntimeStatus>? StatusChanged;

        public event Action<ProviderRefreshStatus>? RefreshStatusChanged;

        public int RefreshCallCount { get; private set; }

        public int ShellFaultCallCount { get; private set; }

        public Func<CancellationToken, Task>? RefreshAsyncCallback { get; set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            _refreshSignal.Release();

            try
            {
                if (RefreshAsyncCallback is not null)
                {
                    await RefreshAsyncCallback(cancellationToken);
                }
            }
            catch
            {
                Status = ApplicationRuntimeStatus.RefreshFaulted;
                StatusChanged?.Invoke(Status);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ReportShellFault()
        {
            ShellFaultCallCount++;
            Status = ApplicationRuntimeStatus.ShellFaulted;
            StatusChanged?.Invoke(Status);
        }

        public void SetRefreshStatus(ProviderRefreshStatus status)
        {
            RefreshStatus = status;
            RefreshStatusChanged?.Invoke(status);
        }

        public async Task WaitForRefreshCountAsync(int expectedCount)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            while (RefreshCallCount < expectedCount)
            {
                await _refreshSignal.WaitAsync(timeout.Token);
            }
        }
    }
}
