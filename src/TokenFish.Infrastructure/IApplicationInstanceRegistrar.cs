namespace TokenFish.Infrastructure;

public interface IApplicationInstanceRegistrar
{
    IApplicationInstanceRegistration FindOrRegister(string key);

    IApplicationInstanceActivationArguments GetActivatedArguments();
}
