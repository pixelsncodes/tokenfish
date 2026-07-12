using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class ApplicationInstanceStartupCoordinatorTests
{
    [Fact]
    public async Task PrimaryInstanceContinuesStartup()
    {
        var registration = new RecordingInstanceRegistration { IsCurrent = true };
        var registrar = new RecordingInstanceRegistrar(registration);
        var coordinator = new ApplicationInstanceStartupCoordinator(registrar);

        var result = await coordinator.DecideStartupAsync(CancellationToken.None);

        Assert.Equal(ApplicationInstanceStartupKind.Primary, result.Kind);
        Assert.Equal(ApplicationInstanceStartupIssue.None, result.Issue);
        Assert.Equal(ApplicationInstanceStartupCoordinator.RegistrationKey, registrar.RegisteredKey);
        Assert.Equal(1, registrar.FindOrRegisterCallCount);
        Assert.Equal(0, registrar.GetActivatedArgumentsCallCount);
        Assert.Equal(0, registration.RedirectCallCount);
    }

    [Fact]
    public async Task SecondaryInstanceRedirectsExactlyOnce()
    {
        var registration = new RecordingInstanceRegistration { IsCurrent = false };
        var registrar = new RecordingInstanceRegistrar(registration);
        var coordinator = new ApplicationInstanceStartupCoordinator(registrar);

        var result = await coordinator.DecideStartupAsync(CancellationToken.None);

        Assert.Equal(ApplicationInstanceStartupKind.RedirectedSecondary, result.Kind);
        Assert.Equal(ApplicationInstanceStartupIssue.None, result.Issue);
        Assert.Equal(1, registrar.FindOrRegisterCallCount);
        Assert.Equal(1, registrar.GetActivatedArgumentsCallCount);
        Assert.Equal(1, registration.RedirectCallCount);
        Assert.Same(registrar.ActivationArguments, registration.RedirectedArguments);
    }

    [Fact]
    public async Task SecondaryInstanceDoesNotInitializePrimaryOwnedServices()
    {
        var registration = new RecordingInstanceRegistration { IsCurrent = false };
        var registrar = new RecordingInstanceRegistrar(registration);
        var coordinator = new ApplicationInstanceStartupCoordinator(registrar);
        var runner = new ApplicationInstanceStartupRunner(coordinator);
        var primaryStartupCallCount = 0;

        var result = await runner.RunAsync(
            () =>
            {
                primaryStartupCallCount++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(ApplicationInstanceStartupKind.RedirectedSecondary, result.Kind);
        Assert.Equal(0, primaryStartupCallCount);
        Assert.Equal(1, registration.RedirectCallCount);
    }

    [Fact]
    public async Task RedirectionFailureProducesClosedSanitizedStartupResult()
    {
        var registration = new RecordingInstanceRegistration
        {
            IsCurrent = false,
            RedirectAsyncCallback = (_, _) =>
                Task.FromException(new InvalidOperationException("C:\\Users\\pixel\\secret"))
        };
        var registrar = new RecordingInstanceRegistrar(registration);
        var coordinator = new ApplicationInstanceStartupCoordinator(registrar);

        var result = await coordinator.DecideStartupAsync(CancellationToken.None);

        Assert.Equal(ApplicationInstanceStartupKind.Closed, result.Kind);
        Assert.Equal(ApplicationInstanceStartupIssue.RedirectionFailed, result.Issue);
        Assert.DoesNotContain("secret", result.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Users", result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedDecisionCallsCannotCreateMultiplePrimaryOwners()
    {
        var registration = new RecordingInstanceRegistration { IsCurrent = true };
        var registrar = new RecordingInstanceRegistrar(registration);
        var coordinator = new ApplicationInstanceStartupCoordinator(registrar);

        var first = await coordinator.DecideStartupAsync(CancellationToken.None);
        var second = await coordinator.DecideStartupAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(ApplicationInstanceStartupKind.Primary, first.Kind);
        Assert.Equal(1, registrar.FindOrRegisterCallCount);
    }

    [Fact]
    public async Task CancellationDuringRedirectionIsHandledDeterministically()
    {
        using var cancellation = new CancellationTokenSource();
        var redirectStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = new RecordingInstanceRegistration
        {
            IsCurrent = false,
            RedirectAsyncCallback = async (_, cancellationToken) =>
            {
                redirectStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        };
        var registrar = new RecordingInstanceRegistrar(registration);
        var coordinator = new ApplicationInstanceStartupCoordinator(registrar);

        var decision = coordinator.DecideStartupAsync(cancellation.Token);
        await redirectStarted.Task;
        cancellation.Cancel();

        var result = await decision;

        Assert.Equal(ApplicationInstanceStartupKind.Closed, result.Kind);
        Assert.Equal(ApplicationInstanceStartupIssue.RedirectionCanceled, result.Issue);
        Assert.Equal(1, registration.RedirectCallCount);
    }

    private sealed class RecordingInstanceRegistrar : IApplicationInstanceRegistrar
    {
        private readonly IApplicationInstanceRegistration _registration;

        public RecordingInstanceRegistrar(IApplicationInstanceRegistration registration)
        {
            _registration = registration;
        }

        public RecordingActivationArguments ActivationArguments { get; } = new();

        public string? RegisteredKey { get; private set; }

        public int FindOrRegisterCallCount { get; private set; }

        public int GetActivatedArgumentsCallCount { get; private set; }

        public IApplicationInstanceRegistration FindOrRegister(string key)
        {
            FindOrRegisterCallCount++;
            RegisteredKey = key;
            return _registration;
        }

        public IApplicationInstanceActivationArguments GetActivatedArguments()
        {
            GetActivatedArgumentsCallCount++;
            return ActivationArguments;
        }
    }

    private sealed class RecordingInstanceRegistration : IApplicationInstanceRegistration
    {
        private Action? _activationHandler;

        public bool IsCurrent { get; init; }

        public int RedirectCallCount { get; private set; }

        public IApplicationInstanceActivationArguments? RedirectedArguments { get; private set; }

        public Func<IApplicationInstanceActivationArguments, CancellationToken, Task>?
            RedirectAsyncCallback { get; init; }

        public Task RedirectActivationToAsync(
            IApplicationInstanceActivationArguments activationArguments,
            CancellationToken cancellationToken)
        {
            RedirectCallCount++;
            RedirectedArguments = activationArguments;
            return RedirectAsyncCallback?.Invoke(activationArguments, cancellationToken) ??
                Task.CompletedTask;
        }

        public IDisposable SubscribeActivated(Action activationHandler)
        {
            _activationHandler += activationHandler;
            return new CallbackDisposable(() => _activationHandler -= activationHandler);
        }

        public void RaiseActivated() => _activationHandler?.Invoke();
    }

    private sealed class RecordingActivationArguments : IApplicationInstanceActivationArguments
    {
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private readonly Action _dispose;

        public CallbackDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose() => _dispose();
    }
}
