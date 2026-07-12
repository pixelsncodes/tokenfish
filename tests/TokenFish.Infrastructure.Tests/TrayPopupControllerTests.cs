using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class TrayPopupControllerTests
{
    [Fact]
    public async Task RepeatedPopupActivationTogglesVisibility()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        using var controller = CreateController(shell, timer);

        await controller.ToggleAsync(CancellationToken.None);
        await controller.ToggleAsync(CancellationToken.None);

        Assert.False(shell.IsVisible);
        Assert.Equal(1, shell.ShowCallCount);
        Assert.Equal(1, shell.HideCallCount);
    }

    [Fact]
    public async Task DeactivationHidesPopupWithoutRequestingExit()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var exitRequested = false;
        using var controller = CreateController(shell, timer, exitAsync: () =>
        {
            exitRequested = true;
            return Task.CompletedTask;
        });

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseDeactivated();
        await shell.WaitForHideAsync();

        Assert.False(shell.IsVisible);
        Assert.False(exitRequested);
    }

    [Fact]
    public async Task ExitRemainsSeparateExplicitCommand()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var exitCount = 0;
        using var controller = CreateController(shell, timer, exitAsync: () =>
        {
            exitCount++;
            return Task.CompletedTask;
        });

        await controller.ToggleAsync(CancellationToken.None);
        shell.RaiseCloseRequested();
        await shell.WaitForHideAsync();

        Assert.Equal(0, exitCount);
    }

    [Fact]
    public async Task VisibleUpdateTimerStartsAndStopsWithPopupVisibility()
    {
        var shell = new RecordingPopupShell();
        var timer = new RecordingPopupUpdateTimer();
        var refreshCount = 0;
        using var controller = CreateController(shell, timer, refreshAsync: _ =>
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

    private static TrayPopupController CreateController(
        RecordingPopupShell shell,
        RecordingPopupUpdateTimer timer,
        Func<CancellationToken, Task>? refreshAsync = null,
        Func<Task>? exitAsync = null)
    {
        _ = exitAsync;
        return new TrayPopupController(
            shell,
            timer,
            refreshAsync ?? (_ => Task.CompletedTask));
    }

    private sealed class RecordingPopupShell : IPopupShell
    {
        private readonly SemaphoreSlim _hideSignal = new(0);

        public bool IsVisible { get; private set; }

        public event Action? Deactivated;

        public event Action? CloseRequested;

        public int ShowCallCount { get; private set; }

        public int HideCallCount { get; private set; }

        public Task ShowAsync(CancellationToken cancellationToken)
        {
            ShowCallCount++;
            IsVisible = true;
            return Task.CompletedTask;
        }

        public Task HideAsync(CancellationToken cancellationToken)
        {
            HideCallCount++;
            IsVisible = false;
            _hideSignal.Release();
            return Task.CompletedTask;
        }

        public void RaiseDeactivated() => Deactivated?.Invoke();

        public void RaiseCloseRequested() => CloseRequested?.Invoke();

        public async Task WaitForHideAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _hideSignal.WaitAsync(timeout.Token);
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
