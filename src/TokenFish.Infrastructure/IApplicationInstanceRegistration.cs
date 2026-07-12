namespace TokenFish.Infrastructure;

public interface IApplicationInstanceRegistration : IApplicationRelaunchActivationSource
{
    bool IsCurrent { get; }

    Task RedirectActivationToAsync(
        IApplicationInstanceActivationArguments activationArguments,
        CancellationToken cancellationToken);
}
