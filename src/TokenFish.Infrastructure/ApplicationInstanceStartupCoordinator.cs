namespace TokenFish.Infrastructure;

public sealed class ApplicationInstanceStartupCoordinator : IApplicationRelaunchActivationSource
{
    public const string RegistrationKey = "TokenFish.Application.SingleInstance.v1";

    private readonly IApplicationInstanceRegistrar _registrar;
    private readonly SemaphoreSlim _decisionGate = new(1, 1);
    private readonly object _sync = new();

    private ApplicationInstanceStartupResult? _decision;
    private IApplicationInstanceRegistration? _primaryRegistration;

    public ApplicationInstanceStartupCoordinator(IApplicationInstanceRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);

        _registrar = registrar;
    }

    public async Task<ApplicationInstanceStartupResult> DecideStartupAsync(
        CancellationToken cancellationToken)
    {
        if (_decision is not null)
        {
            return _decision;
        }

        try
        {
            await _decisionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApplicationInstanceStartupResult.Closed(
                ApplicationInstanceStartupIssue.RedirectionCanceled);
        }

        try
        {
            if (_decision is not null)
            {
                return _decision;
            }

            _decision = await DecideStartupCoreAsync(cancellationToken).ConfigureAwait(false);
            return _decision;
        }
        finally
        {
            _decisionGate.Release();
        }
    }

    private async Task<ApplicationInstanceStartupResult> DecideStartupCoreAsync(
        CancellationToken cancellationToken)
    {
        IApplicationInstanceRegistration registration;
        try
        {
            registration = _registrar.FindOrRegister(RegistrationKey);
        }
        catch
        {
            return ApplicationInstanceStartupResult.Closed(
                ApplicationInstanceStartupIssue.RegistrationFailed);
        }

        if (registration.IsCurrent)
        {
            lock (_sync)
            {
                _primaryRegistration ??= registration;
            }

            return ApplicationInstanceStartupResult.Primary;
        }

        IApplicationInstanceActivationArguments activationArguments;
        try
        {
            activationArguments = _registrar.GetActivatedArguments();
            await registration
                .RedirectActivationToAsync(activationArguments, cancellationToken)
                .ConfigureAwait(false);

            return ApplicationInstanceStartupResult.RedirectedSecondary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApplicationInstanceStartupResult.Closed(
                ApplicationInstanceStartupIssue.RedirectionCanceled);
        }
        catch
        {
            return ApplicationInstanceStartupResult.Closed(
                ApplicationInstanceStartupIssue.RedirectionFailed);
        }
    }

    public IDisposable SubscribeActivated(Action activationHandler)
    {
        ArgumentNullException.ThrowIfNull(activationHandler);

        IApplicationInstanceRegistration? primaryRegistration;
        lock (_sync)
        {
            primaryRegistration = _primaryRegistration;
        }

        if (primaryRegistration is null)
        {
            throw new InvalidOperationException(
                "Relaunch activation can only be subscribed after primary ownership is established.");
        }

        return primaryRegistration.SubscribeActivated(activationHandler);
    }
}
