using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class ManualRefreshCommandTests
{
    [Fact]
    public void CommandIsInitiallyAvailableWhenLifecycleIsIdle()
    {
        using var command = new ManualRefreshCommand(new RecordingRuntimeHost());

        Assert.Equal(ManualRefreshCommandState.Available, command.State);
    }

    [Fact]
    public void CommandBecomesUnavailableWhileAnyRefreshIsActive()
    {
        var host = new RecordingRuntimeHost();
        using var command = new ManualRefreshCommand(host);
        var states = new List<ManualRefreshCommandState>();
        command.StateChanged += states.Add;

        host.SetRefreshStatus(RefreshingStatus(version: 1));

        Assert.Equal(ManualRefreshCommandState.Refreshing, command.State);
        Assert.Equal([ManualRefreshCommandState.Refreshing], states);
    }

    [Fact]
    public void InitialRefreshDisablesManualCommand()
    {
        var host = new RecordingRuntimeHost();
        using var command = new ManualRefreshCommand(host);

        host.SetRefreshStatus(RefreshingStatus(version: 1));

        Assert.False(command.State.IsEnabled);
    }

    [Fact]
    public void ScheduledRefreshDisablesManualCommand()
    {
        var host = new RecordingRuntimeHost();
        using var command = new ManualRefreshCommand(host);

        host.SetRefreshStatus(SucceededStatus(version: 2));
        host.SetRefreshStatus(RefreshingStatus(version: 3));

        Assert.False(command.State.IsEnabled);
    }

    [Fact]
    public async Task AcceptedManualRequestInvokesRuntimeRefreshExactlyOnce()
    {
        var host = new RecordingRuntimeHost();
        using var command = new ManualRefreshCommand(host);

        await command.RequestAsync(CancellationToken.None);

        Assert.Equal(1, host.RefreshCallCount);
        Assert.Equal(ManualRefreshCommandState.Available, command.State);
    }

    [Fact]
    public async Task RapidRepeatedRequestsInvokeRuntimeRefreshOnce()
    {
        var host = CreateBlockingHost();
        using var command = new ManualRefreshCommand(host);

        var first = command.RequestAsync(CancellationToken.None);
        await host.WaitForRefreshCountAsync(1);
        await command.RequestAsync(CancellationToken.None);

        host.CompleteRefresh();
        await first;

        Assert.Equal(1, host.RefreshCallCount);
    }

    [Fact]
    public async Task TwoSimultaneousRequestsInvokeRuntimeRefreshOnce()
    {
        var host = CreateBlockingHost();
        using var command = new ManualRefreshCommand(host);

        var first = command.RequestAsync(CancellationToken.None);
        var second = command.RequestAsync(CancellationToken.None);

        await host.WaitForRefreshCountAsync(1);
        host.CompleteRefresh();
        await Task.WhenAll(first, second);

        Assert.Equal(1, host.RefreshCallCount);
    }

    [Fact]
    public async Task TrayOriginatedAndPopupOriginatedRequestsShareSameGate()
    {
        var host = CreateBlockingHost();
        using var command = new ManualRefreshCommand(host);

        var trayRequest = command.RequestAsync(CancellationToken.None);
        await host.WaitForRefreshCountAsync(1);
        var popupRequest = command.RequestAsync(CancellationToken.None);

        host.CompleteRefresh();
        await Task.WhenAll(trayRequest, popupRequest);

        Assert.Equal(1, host.RefreshCallCount);
    }

    [Fact]
    public async Task TrayRequestFollowedByPopupRequestDoesNotQueue()
    {
        var host = CreateBlockingHost();
        using var command = new ManualRefreshCommand(host);

        var trayRequest = command.RequestAsync(CancellationToken.None);
        await host.WaitForRefreshCountAsync(1);
        await command.RequestAsync(CancellationToken.None);

        host.CompleteRefresh();
        await trayRequest;

        Assert.Equal(1, host.RefreshCallCount);
    }

    [Fact]
    public async Task PopupRequestFollowedByTrayRequestDoesNotQueue()
    {
        var host = CreateBlockingHost();
        using var command = new ManualRefreshCommand(host);

        var popupRequest = command.RequestAsync(CancellationToken.None);
        await host.WaitForRefreshCountAsync(1);
        await command.RequestAsync(CancellationToken.None);

        host.CompleteRefresh();
        await popupRequest;

        Assert.Equal(1, host.RefreshCallCount);
    }

    [Fact]
    public async Task CommandBecomesAvailableAfterSuccess()
    {
        var host = CreateBlockingHost();
        using var command = new ManualRefreshCommand(host);

        var request = command.RequestAsync(CancellationToken.None);
        await host.WaitForRefreshCountAsync(1);
        Assert.Equal(ManualRefreshCommandState.Refreshing, command.State);

        host.SetRefreshStatus(SucceededStatus(version: 2));
        host.CompleteRefresh();
        await request;

        Assert.Equal(ManualRefreshCommandState.Available, command.State);
    }

    [Fact]
    public async Task CommandBecomesAvailableAfterSafeFailure()
    {
        var host = CreateBlockingHost();
        using var command = new ManualRefreshCommand(host);

        var request = command.RequestAsync(CancellationToken.None);
        await host.WaitForRefreshCountAsync(1);

        host.SetRefreshStatus(FailedStatus(version: 2));
        host.FailRefresh(new InvalidOperationException("secret C:\\Users\\pixel"));
        await request;

        Assert.Equal(ManualRefreshCommandState.Available, command.State);
        Assert.DoesNotContain("secret", command.State.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Users", command.State.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShutdownCancellationDoesNotLeaveCommandUnavailable()
    {
        var host = CreateBlockingHost();
        using var command = new ManualRefreshCommand(host);

        var request = command.RequestAsync(CancellationToken.None);
        await host.WaitForRefreshCountAsync(1);

        host.FailRefresh(new OperationCanceledException());
        await request;

        Assert.Equal(ManualRefreshCommandState.Available, command.State);
    }

    [Fact]
    public async Task UnexpectedTaskCompletionAfterShutdownIsIgnoredSafely()
    {
        var host = CreateBlockingHost();
        var command = new ManualRefreshCommand(host);
        var request = command.RequestAsync(CancellationToken.None);
        await host.WaitForRefreshCountAsync(1);

        command.Dispose();
        host.SetRefreshStatus(SucceededStatus(version: 2));
        host.CompleteRefresh();
        await request;

        Assert.Equal(ManualRefreshCommandState.Refreshing, command.State);
    }

    [Fact]
    public void StateObserversDoNotReceiveDuplicateUnchangedNotifications()
    {
        var host = new RecordingRuntimeHost();
        using var command = new ManualRefreshCommand(host);
        var states = new List<ManualRefreshCommandState>();
        command.StateChanged += states.Add;

        host.SetRefreshStatus(RefreshingStatus(version: 1));
        host.SetRefreshStatus(RefreshingStatus(version: 2));
        host.SetRefreshStatus(SucceededStatus(version: 3));
        host.SetRefreshStatus(SucceededStatus(version: 4));

        Assert.Equal(
            [ManualRefreshCommandState.Refreshing, ManualRefreshCommandState.Available],
            states);
    }

    [Fact]
    public void DisposalRemovesSubscriptions()
    {
        var host = new RecordingRuntimeHost();
        var command = new ManualRefreshCommand(host);
        command.Dispose();

        host.SetRefreshStatus(RefreshingStatus(version: 1));

        Assert.Equal(ManualRefreshCommandState.Available, command.State);
    }

    [Fact]
    public void RepeatedDisposalIsSafe()
    {
        var host = new RecordingRuntimeHost();
        var command = new ManualRefreshCommand(host);

        command.Dispose();
        command.Dispose();

        Assert.Equal(1, host.UnsubscribeCallCount);
    }

    private static RecordingRuntimeHost CreateBlockingHost()
    {
        var host = new RecordingRuntimeHost();
        host.RefreshAsyncCallback = _ => host.PendingRefreshTask;
        return host;
    }

    private static ProviderRefreshStatus RefreshingStatus(long version) =>
        new(
            IsRefreshActive: true,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 13, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: null,
            ProviderRefreshOutcome.Refreshing,
            Version: version);

    private static ProviderRefreshStatus SucceededStatus(long version) =>
        new(
            IsRefreshActive: false,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 13, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: new DateTimeOffset(2026, 7, 13, 8, 0, 0, TimeSpan.Zero),
            ProviderRefreshOutcome.Succeeded,
            Version: version);

    private static ProviderRefreshStatus FailedStatus(long version) =>
        new(
            IsRefreshActive: false,
            LatestAttemptedAtUtc: new DateTimeOffset(2026, 7, 13, 8, 0, 0, TimeSpan.Zero),
            LatestSucceededAtUtc: null,
            ProviderRefreshOutcome.Failed,
            Version: version);

    private sealed class RecordingRuntimeHost : IApplicationRuntimeHost
    {
        private readonly SemaphoreSlim _refreshSignal = new(0);
        private event Action<ProviderRefreshStatus>? RefreshStatusChangedInner;
        private TaskCompletionSource _pendingRefresh =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ApplicationRuntimeStatus Status { get; private set; } =
            ApplicationRuntimeStatus.Running;

        public ProviderRefreshStatus RefreshStatus { get; private set; } =
            ProviderRefreshStatus.Initial;

        public AppSettings? CurrentSettings { get; }

        public event Action<ApplicationRuntimeStatus>? StatusChanged;

        public event Action<ProviderRefreshStatus>? RefreshStatusChanged
        {
            add => RefreshStatusChangedInner += value;
            remove
            {
                UnsubscribeCallCount++;
                RefreshStatusChangedInner -= value;
            }
        }

        public int RefreshCallCount { get; private set; }

        public int UnsubscribeCallCount { get; private set; }

        public Func<CancellationToken, Task>? RefreshAsyncCallback { get; set; }

        public Task PendingRefreshTask => _pendingRefresh.Task;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            RefreshCallCount++;
            _refreshSignal.Release();

            if (RefreshAsyncCallback is not null)
            {
                await RefreshAsyncCallback(cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ReportShellFault()
        {
            Status = ApplicationRuntimeStatus.ShellFaulted;
            StatusChanged?.Invoke(Status);
        }

        public void SetRefreshStatus(ProviderRefreshStatus status)
        {
            RefreshStatus = status;
            RefreshStatusChangedInner?.Invoke(status);
        }

        public void CompleteRefresh()
        {
            _pendingRefresh.TrySetResult();
            _pendingRefresh = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void FailRefresh(Exception exception)
        {
            _pendingRefresh.TrySetException(exception);
            _pendingRefresh = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
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
