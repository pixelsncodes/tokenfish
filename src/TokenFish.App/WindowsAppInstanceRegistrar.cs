using Microsoft.Windows.AppLifecycle;
using TokenFish.Infrastructure;

namespace TokenFish.App;

internal sealed class WindowsAppInstanceRegistrar : IApplicationInstanceRegistrar
{
    public IApplicationInstanceRegistration FindOrRegister(string key) =>
        new WindowsAppInstanceRegistration(AppInstance.FindOrRegisterForKey(key));

    public IApplicationInstanceActivationArguments GetActivatedArguments() =>
        new WindowsApplicationInstanceActivationArguments(
            AppInstance.GetCurrent().GetActivatedEventArgs());
}

internal sealed class WindowsAppInstanceRegistration : IApplicationInstanceRegistration
{
    private readonly AppInstance _instance;

    public WindowsAppInstanceRegistration(AppInstance instance)
    {
        _instance = instance;
    }

    public bool IsCurrent => _instance.IsCurrent;

    public Task RedirectActivationToAsync(
        IApplicationInstanceActivationArguments activationArguments,
        CancellationToken cancellationToken)
    {
        if (activationArguments is not WindowsApplicationInstanceActivationArguments windowsArguments)
        {
            throw new ArgumentException(
                "Activation arguments were not created by the Windows app instance registrar.",
                nameof(activationArguments));
        }

        return _instance
            .RedirectActivationToAsync(windowsArguments.Arguments)
            .AsTask(cancellationToken);
    }
}

internal sealed class WindowsApplicationInstanceActivationArguments :
    IApplicationInstanceActivationArguments
{
    public WindowsApplicationInstanceActivationArguments(AppActivationArguments arguments)
    {
        Arguments = arguments;
    }

    public AppActivationArguments Arguments { get; }
}
