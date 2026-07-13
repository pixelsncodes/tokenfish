using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Core.Settings;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class TokenFishApplicationRuntimeHostTests
{
    [Fact]
    public async Task SettingsAreLoadedBeforeServiceConstruction()
    {
        var settingsLoaded = false;
        var store = new RecordingSettingsStore
        {
            OnLoad = () => settingsLoaded = true
        };
        var lifecycle = new RecordingRefreshLifecycle();
        var services = new RecordingRuntimeServices(lifecycle);
        var host = CreateHost(store, (_, _) =>
        {
            Assert.True(settingsLoaded);
            return services;
        });

        await host.StartAsync(CancellationToken.None);

        Assert.Equal(1, store.LoadCallCount);
        Assert.Equal(1, lifecycle.StartCallCount);
    }

    [Fact]
    public async Task DuplicateStartupConstructsServicesOnceAndStartsLifecycleOnce()
    {
        var createCallCount = 0;
        var lifecycle = new RecordingRefreshLifecycle();
        var services = new RecordingRuntimeServices(lifecycle);
        var host = CreateHost(createServices: (_, _) =>
        {
            createCallCount++;
            return services;
        });

        await host.StartAsync(CancellationToken.None);
        await host.StartAsync(CancellationToken.None);

        Assert.Equal(1, createCallCount);
        Assert.Equal(1, lifecycle.StartCallCount);
        Assert.Equal(ApplicationRuntimeStatus.Running, host.Status);
    }

    [Fact]
    public async Task StartupFailureMapsToClosedGenericIssue()
    {
        var lifecycle = new RecordingRefreshLifecycle
        {
            StartAsyncCallback = _ =>
                Task.FromException(new InvalidOperationException("secret path C:\\Users\\pixel"))
        };
        var host = CreateHost(createServices: (_, _) => new RecordingRuntimeServices(lifecycle));

        await host.StartAsync(CancellationToken.None);

        Assert.Equal(ApplicationRuntimeState.Faulted, host.Status.State);
        Assert.Equal(ApplicationRuntimeIssue.StartupFailed, host.Status.Issue);
        Assert.DoesNotContain("secret", host.Status.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Users", host.Status.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExpectedShutdownCancellationDoesNotMapToFault()
    {
        var store = new RecordingSettingsStore
        {
            LoadAsyncCallback = cancellationToken =>
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    .ContinueWith(
                        _ => new AppSettings(),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnCanceled,
                        TaskScheduler.Default)
        };
        var host = CreateHost(store);

        var startup = host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
        await startup;

        Assert.Equal(ApplicationRuntimeStatus.Stopped, host.Status);
    }

    [Fact]
    public async Task ShutdownStopsLifecycleBeforeDisposingServices()
    {
        var events = new List<string>();
        var lifecycle = new RecordingRefreshLifecycle
        {
            OnStop = () => events.Add("stop")
        };
        var services = new RecordingRuntimeServices(lifecycle)
        {
            OnDispose = () => events.Add("dispose")
        };
        var host = CreateHost(createServices: (_, _) => services);

        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);

        Assert.Equal(["stop", "dispose"], events);
        Assert.Equal(1, lifecycle.StopCallCount);
        Assert.Equal(1, services.DisposeCallCount);
    }

    [Fact]
    public async Task DuplicateShutdownIsSafe()
    {
        var lifecycle = new RecordingRefreshLifecycle();
        var services = new RecordingRuntimeServices(lifecycle);
        var host = CreateHost(createServices: (_, _) => services);

        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);

        Assert.Equal(1, lifecycle.StopCallCount);
        Assert.Equal(1, services.DisposeCallCount);
        Assert.Equal(ApplicationRuntimeStatus.Stopped, host.Status);
    }

    [Fact]
    public async Task ExitDuringStartupCompletesSafely()
    {
        var store = new BlockingSettingsStore();
        var host = CreateHost(store);

        var startup = host.StartAsync(CancellationToken.None);
        await store.WaitForLoadAsync();

        var shutdown = host.StopAsync(CancellationToken.None);
        store.Complete(new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly });

        await startup;
        await shutdown;

        Assert.Null(host.Services);
        Assert.Equal(ApplicationRuntimeStatus.Stopped, host.Status);
    }

    [Fact]
    public async Task ScheduledLifecycleFailureIsObservedAsGenericIssue()
    {
        var lifecycle = new RecordingRefreshLifecycle();
        var host = CreateHost(createServices: (_, _) => new RecordingRuntimeServices(lifecycle));
        var statuses = new List<ApplicationRuntimeStatus>();
        host.StatusChanged += statuses.Add;

        await host.StartAsync(CancellationToken.None);

        lifecycle.FaultCompletion(new InvalidOperationException("raw provider payload"));
        await WaitForStatusAsync(host, ApplicationRuntimeIssue.RefreshFailed);

        Assert.Contains(statuses, status => status.Issue == ApplicationRuntimeIssue.RefreshFailed);
        Assert.DoesNotContain("payload", host.Status.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisabledProviderLazinessRemainsIntact()
    {
        var createCallCount = 0;
        await using var services = TokenFishApplicationServices.Create(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly },
            "1.0.0",
            (_, _) =>
            {
                createCallCount++;
                throw new InvalidOperationException("Codex should not be constructed.");
            });

        Assert.Null(services.CodexUsageCollector);
        Assert.Equal(0, createCallCount);
    }

    private static TokenFishApplicationRuntimeHost CreateHost(
        IAppSettingsStore? settingsStore = null,
        Func<AppSettings, string, IApplicationRuntimeServices>? createServices = null)
    {
        Func<AppSettings, string, IApplicationRuntimeServices> defaultFactory =
            (_, _) => new RecordingRuntimeServices(new RecordingRefreshLifecycle());

        return new TokenFishApplicationRuntimeHost(
            settingsStore ?? new RecordingSettingsStore(),
            "1.0.0",
            createServices ?? defaultFactory);
    }

    private static TokenFishApplicationRuntimeHost CreateHost(
        Func<AppSettings, string, IApplicationRuntimeServices> createServices) =>
        CreateHost(null, createServices);

    private static async Task WaitForStatusAsync(
        TokenFishApplicationRuntimeHost host,
        ApplicationRuntimeIssue issue)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (host.Status.Issue != issue)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    private sealed class RecordingSettingsStore : IAppSettingsStore
    {
        public int LoadCallCount { get; private set; }

        public Action? OnLoad { get; init; }

        public Func<CancellationToken, Task<AppSettings>>? LoadAsyncCallback { get; init; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
        {
            LoadCallCount++;
            OnLoad?.Invoke();

            return LoadAsyncCallback?.Invoke(cancellationToken) ??
                Task.FromResult(new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly });
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class BlockingSettingsStore : IAppSettingsStore
    {
        private readonly TaskCompletionSource _loadStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<AppSettings> _loadCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
        {
            _loadStarted.SetResult();
            cancellationToken.Register(() => _loadCompletion.TrySetCanceled(cancellationToken));
            return _loadCompletion.Task;
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task WaitForLoadAsync() => _loadStarted.Task;

        public void Complete(AppSettings settings) => _loadCompletion.TrySetResult(settings);
    }

    private sealed class RecordingRuntimeServices : IApplicationRuntimeServices
    {
        public RecordingRuntimeServices(IProviderRefreshLifecycle lifecycle)
        {
            ProviderRefreshLifecycle = lifecycle;
        }

        public IProviderRefreshLifecycle ProviderRefreshLifecycle { get; }

        public IProviderRuntimeSnapshotStore ProviderRuntimeSnapshotStore { get; } =
            new InMemoryProviderRuntimeSnapshotStore(TimeProvider.System, TimeSpan.FromMinutes(5));

        public int DisposeCallCount { get; private set; }

        public Action? OnDispose { get; init; }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            OnDispose?.Invoke();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingRefreshLifecycle : IProviderRefreshLifecycle
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public Func<CancellationToken, Task>? StartAsyncCallback { get; init; }

        public Action? OnStop { get; init; }

        public ProviderRefreshStatus RefreshStatus { get; private set; } =
            ProviderRefreshStatus.Initial;

        public event Action<ProviderRefreshStatus>? RefreshStatusChanged
        {
            add { }
            remove { }
        }

        public Task Completion => _completion.Task;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCallCount++;
            return StartAsyncCallback?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProviderUsageSnapshot>> RefreshAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderUsageSnapshot>>([]);

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCallCount++;
            OnStop?.Invoke();
            _completion.TrySetCanceled(cancellationToken);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _completion.TrySetCanceled();
            return ValueTask.CompletedTask;
        }

        public void FaultCompletion(Exception exception) => _completion.TrySetException(exception);
    }
}
