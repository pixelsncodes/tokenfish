namespace TokenFish.Infrastructure;

public interface IApplicationRelaunchActivationSource
{
    IDisposable SubscribeActivated(Action activationHandler);
}
