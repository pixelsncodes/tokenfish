using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class ApplicationShutdownCoordinatorTests
{
    [Fact]
    public async Task ShutdownWaitsForOwnedCleanupBeforeCompletingApplicationLifetime()
    {
        var events = new List<string>();
        var cleanupRelease = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ApplicationShutdownCoordinator(
            async _ =>
            {
                events.Add("cleanup-started");
                await cleanupRelease.Task;
                events.Add("cleanup-completed");
            },
            () => events.Add("application-exit"),
            () => events.Add("fault"));

        var shutdown = coordinator.ShutdownAsync(CancellationToken.None);
        await WaitForEventAsync(events, "cleanup-started");

        Assert.False(shutdown.IsCompleted);
        Assert.DoesNotContain("application-exit", events);

        cleanupRelease.SetResult();
        await shutdown;

        Assert.Equal(["cleanup-started", "cleanup-completed", "application-exit"], events);
    }

    [Fact]
    public async Task RepeatedShutdownRequestsRunCleanupAndApplicationExitOnce()
    {
        var cleanupCallCount = 0;
        var applicationExitCallCount = 0;
        var coordinator = new ApplicationShutdownCoordinator(
            _ =>
            {
                cleanupCallCount++;
                return Task.CompletedTask;
            },
            () => applicationExitCallCount++,
            () => { });

        await coordinator.ShutdownAsync(CancellationToken.None);
        await coordinator.ShutdownAsync(CancellationToken.None);

        Assert.True(coordinator.IsShutdownStarted);
        Assert.Equal(1, cleanupCallCount);
        Assert.Equal(1, applicationExitCallCount);
    }

    [Fact]
    public async Task ActivationSubscriptionCanBeRemovedBeforeApplicationLifetimeEnds()
    {
        var events = new List<string>();
        var coordinator = new ApplicationShutdownCoordinator(
            _ =>
            {
                events.Add("unsubscribe-activation");
                events.Add("cleanup-owned-resources");
                return Task.CompletedTask;
            },
            () => events.Add("application-exit"),
            () => events.Add("fault"));

        await coordinator.ShutdownAsync(CancellationToken.None);

        Assert.Equal(
            ["unsubscribe-activation", "cleanup-owned-resources", "application-exit"],
            events);
    }

    [Fact]
    public async Task CleanupFailureReportsGenericFaultAndStillCompletesApplicationLifetime()
    {
        var faultCallCount = 0;
        var applicationExitCallCount = 0;
        var coordinator = new ApplicationShutdownCoordinator(
            _ => Task.FromException(new InvalidOperationException("C:\\Users\\pixel\\secret")),
            () => applicationExitCallCount++,
            () => faultCallCount++);

        await coordinator.ShutdownAsync(CancellationToken.None);

        Assert.Equal(1, faultCallCount);
        Assert.Equal(1, applicationExitCallCount);
    }

    private static async Task WaitForEventAsync(IReadOnlyCollection<string> events, string value)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!events.Contains(value))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }
}
