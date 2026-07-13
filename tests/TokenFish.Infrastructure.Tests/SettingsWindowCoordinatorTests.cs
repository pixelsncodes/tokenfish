using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class SettingsWindowCoordinatorTests
{
    [Fact]
    public void RepeatedOpenRequestsReuseOneLiveWindow()
    {
        var factory = new RecordingSettingsWindowFactory();
        var coordinator = new SettingsWindowCoordinator(factory.Create);

        coordinator.Open();
        coordinator.Open();

        Assert.Equal(1, factory.CreateCallCount);
        Assert.Equal(1, factory.Windows[0].ShowCallCount);
    }

    [Fact]
    public void ExistingWindowIsActivated()
    {
        var factory = new RecordingSettingsWindowFactory();
        var coordinator = new SettingsWindowCoordinator(factory.Create);

        coordinator.Open();
        coordinator.Open();

        Assert.Equal(2, factory.Windows[0].ActivateCallCount);
    }

    [Fact]
    public void ClosedWindowCanBeRecreated()
    {
        var factory = new RecordingSettingsWindowFactory();
        var coordinator = new SettingsWindowCoordinator(factory.Create);

        coordinator.Open();
        factory.Windows[0].RaiseClosed();
        coordinator.Open();

        Assert.Equal(2, factory.CreateCallCount);
        Assert.Equal(1, factory.Windows[1].ShowCallCount);
    }

    [Fact]
    public void StaleClosedNotificationDoesNotReleaseNewWindow()
    {
        var factory = new RecordingSettingsWindowFactory();
        var coordinator = new SettingsWindowCoordinator(factory.Create);

        coordinator.Open();
        var firstWindow = factory.Windows[0];
        firstWindow.RaiseClosed();
        coordinator.Open();
        firstWindow.RaiseClosed();
        coordinator.Open();

        Assert.Equal(2, factory.CreateCallCount);
        Assert.Equal(2, factory.Windows[1].ActivateCallCount);
    }

    [Fact]
    public void ShutdownClosesWindowOnce()
    {
        var factory = new RecordingSettingsWindowFactory();
        var coordinator = new SettingsWindowCoordinator(factory.Create);

        coordinator.Open();
        coordinator.Shutdown();

        Assert.Equal(1, factory.Windows[0].CloseCallCount);
    }

    [Fact]
    public void RepeatedShutdownIsSafe()
    {
        var factory = new RecordingSettingsWindowFactory();
        var coordinator = new SettingsWindowCoordinator(factory.Create);

        coordinator.Open();
        coordinator.Shutdown();
        coordinator.Shutdown();

        Assert.Equal(1, factory.Windows[0].CloseCallCount);
    }

    [Fact]
    public void SettingsActivationDoesNotTriggerRefreshOrRuntimeConstruction()
    {
        var refreshCallCount = 0;
        var runtimeConstructionCount = 0;
        var factory = new RecordingSettingsWindowFactory();
        var coordinator = new SettingsWindowCoordinator(() =>
        {
            runtimeConstructionCount += 0;
            return factory.Create();
        });

        coordinator.Open();

        Assert.Equal(0, refreshCallCount);
        Assert.Equal(0, runtimeConstructionCount);
    }

    private sealed class RecordingSettingsWindowFactory
    {
        public List<RecordingSettingsWindowShell> Windows { get; } = [];

        public int CreateCallCount { get; private set; }

        public ISettingsWindowShell Create()
        {
            CreateCallCount++;
            var window = new RecordingSettingsWindowShell();
            Windows.Add(window);
            return window;
        }
    }

    private sealed class RecordingSettingsWindowShell : ISettingsWindowShell
    {
        public event Action<ISettingsWindowShell>? Closed;

        public int ShowCallCount { get; private set; }

        public int ActivateCallCount { get; private set; }

        public int CloseCallCount { get; private set; }

        public void Show() => ShowCallCount++;

        public void Activate() => ActivateCallCount++;

        public void Close() => CloseCallCount++;

        public void RaiseClosed() => Closed?.Invoke(this);
    }
}
