using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class TrayPopupControllerTests
{
    [Fact]
    public async Task RepeatedPopupActivationTogglesVisibility()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ToggleAsync(CancellationToken.None);
        await controller.ToggleAsync(CancellationToken.None);

        Assert.False(shell.IsVisible);
        Assert.Equal(1, shell.ShowCallCount);
        Assert.Equal(1, shell.HideCallCount);
    }

    [Fact]
    public async Task RelaunchActivationShowsHiddenPopup()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ShowAsync(CancellationToken.None);

        Assert.True(shell.IsVisible);
        Assert.Equal(1, shell.ShowCallCount);
        Assert.Equal(0, shell.HideCallCount);
        Assert.Equal(1, timer.StartCallCount);
    }

    [Fact]
    public async Task RelaunchActivationKeepsAlreadyVisiblePopupVisible()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ShowAsync(CancellationToken.None);
        await controller.ShowAsync(CancellationToken.None);

        Assert.True(shell.IsVisible);
        Assert.Equal(2, shell.ShowCallCount);
        Assert.Equal(0, shell.HideCallCount);
        Assert.Equal(1, timer.StartCallCount);
    }

    [Fact]
    public async Task RelaunchActivationNeverInvokesTrayToggleHidePath()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ToggleAsync(CancellationToken.None);
        await controller.ShowAsync(CancellationToken.None);

        Assert.True(shell.IsVisible);
        Assert.Equal(0, shell.HideCallCount);
    }

    [Fact]
    public async Task RelaunchShowPreservesTrayActivationToggleBehavior()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ShowAsync(CancellationToken.None);
        await controller.ToggleAsync(CancellationToken.None);

        Assert.False(shell.IsVisible);
        Assert.Equal(1, shell.HideCallCount);
        Assert.Equal(1, timer.StopCallCount);
    }

    [Fact]
    public async Task InitialDeactivationDuringActivationDoesNotHidePopup()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseDeactivated();
        queue.Flush();

        Assert.True(shell.IsVisible);
        Assert.Equal(1, shell.ShowCallCount);
        Assert.Equal(0, shell.HideCallCount);
        Assert.Equal(1, timer.StartCallCount);
        Assert.Equal(0, timer.StopCallCount);
    }

    [Fact]
    public async Task PositiveActivationCompletesGuardAndLaterDeactivationHidesPopup()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseActivated();
        queue.Flush();
        shell.IsForeground = false;
        shell.RaiseDeactivated();
        queue.Flush();

        Assert.False(shell.IsVisible);
        Assert.Equal(1, shell.HideCallCount);
        Assert.Equal(1, timer.StopCallCount);
    }

    [Fact]
    public async Task ImmediateDeactivationAfterPositiveActivationRemainsGuarded()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseActivated();
        shell.IsForeground = false;
        shell.RaiseDeactivated();
        queue.Flush();

        Assert.True(shell.IsVisible);
        Assert.Equal(0, shell.HideCallCount);
    }

    [Fact]
    public async Task EscapeCloseRequestHidesPopupWithoutRequestingExit()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        var exitCount = 0;
        using var controller = CreateController(shell, timer, queue, exitAsync: () =>
        {
            exitCount++;
            return Task.CompletedTask;
        });

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseCloseRequested();
        await shell.WaitForHideAsync();

        Assert.False(shell.IsVisible);
        Assert.Equal(0, exitCount);
        Assert.Equal(1, timer.StopCallCount);
    }

    [Fact]
    public async Task NormalCloseRequestHidesPopupWithoutRequestingExit()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        var exitRequested = false;
        using var controller = CreateController(shell, timer, queue, exitAsync: () =>
        {
            exitRequested = true;
            return Task.CompletedTask;
        });

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseCloseRequested();
        await shell.WaitForHideAsync();

        Assert.False(shell.IsVisible);
        Assert.False(exitRequested);
    }

    [Fact]
    public async Task VisibleUpdateTimerStartsAndStopsWithPopupVisibility()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        var refreshCount = 0;
        using var controller = CreateController(shell, timer, queue, refreshAsync: _ =>
        {
            refreshCount++;
            return Task.CompletedTask;
        });

        await controller.ToggleAsync(CancellationToken.None);
        timer.RaiseTick();
        await controller.ToggleAsync(CancellationToken.None);
        timer.RaiseTick();

        Assert.Equal(1, timer.StartCallCount);
        Assert.Equal(1, timer.StopCallCount);
        Assert.Equal(2, refreshCount);
    }

    [Fact]
    public async Task StaleQueuedDeactivationCannotHideNewlyReopenedPopup()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseActivated();
        queue.Flush();
        shell.IsForeground = false;
        shell.RaiseDeactivated();
        await controller.ToggleAsync(CancellationToken.None);
        await controller.ToggleAsync(CancellationToken.None);

        queue.Flush();

        Assert.True(shell.IsVisible);
        Assert.Equal(2, shell.ShowCallCount);
        Assert.Equal(1, shell.HideCallCount);
    }

    [Fact]
    public async Task ForegroundPopupIgnoresQueuedDeactivation()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var queue = new RecordingPopupActionQueue();
        using var controller = CreateController(shell, timer, queue);

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseActivated();
        queue.Flush();
        shell.RaiseDeactivated();
        queue.Flush();

        Assert.True(shell.IsVisible);
        Assert.Equal(0, shell.HideCallCount);
    }

    private static TrayPopupController CreateController(
        RecordingPopupShell shell,
        RecordingPopupUpdateTimer timer,
        RecordingPopupActionQueue queue,
        Func<CancellationToken, Task>? refreshAsync = null,
        Func<Task>? exitAsync = null)
    {
        _ = exitAsync;
        return new TrayPopupController(
            shell,
            timer,
            refreshAsync ?? (_ => Task.CompletedTask),
            queue);
    }

    private sealed class RecordingPopupShell : IPopupShell
    {
        private readonly SemaphoreSlim _hideSignal = new(0);

        public bool IsVisible { get; private set; }

        public bool IsForeground { get; set; }

        public event Action? Activated;

        public event Action? Deactivated;

        public event Action? CloseRequested;

        public int ShowCallCount { get; private set; }

        public int HideCallCount { get; private set; }

        public Task ShowAsync(CancellationToken cancellationToken)
        {
            ShowCallCount++;
            IsVisible = true;
            IsForeground = true;
            return Task.CompletedTask;
        }

        public Task HideAsync(CancellationToken cancellationToken)
        {
            HideCallCount++;
            IsVisible = false;
            IsForeground = false;
            _hideSignal.Release();
            return Task.CompletedTask;
        }

        public void RaiseActivated() => Activated?.Invoke();

        public void RaiseDeactivated() => Deactivated?.Invoke();

        public void RaiseCloseRequested() => CloseRequested?.Invoke();

        public async Task WaitForHideAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _hideSignal.WaitAsync(timeout.Token);
        }
    }

    private sealed class RecordingPopupActionQueue : IPopupActionQueue
    {
        private readonly Queue<Action> _actions = new();

        public void Enqueue(Action action) => _actions.Enqueue(action);

        public void Flush()
        {
            while (_actions.TryDequeue(out var action))
            {
                action();
            }
        }
    }

    private sealed class RecordingPopupUpdateTimer : IPopupUpdateTimer
    {
        public event Action? Tick;

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public void Start() => StartCallCount++;

        public void Stop() => StopCallCount++;

        public void RaiseTick() => Tick?.Invoke();
    }
}
