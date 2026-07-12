namespace TokenFish.Infrastructure;

public interface IApplicationInstanceRegistration
{
    bool IsCurrent { get; }

    Task RedirectActivationToAsync(
        IApplicationInstanceActivationArguments activationArguments,
        CancellationToken cancellationToken);
}
