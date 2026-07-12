using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TokenFish.Infrastructure;
using WinRT;

namespace TokenFish.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        _ = args;

        ComWrappersSupport.InitializeComWrappers();

        var startupCoordinator = new ApplicationInstanceStartupCoordinator(
            new WindowsAppInstanceRegistrar());
        var startupRunner = new ApplicationInstanceStartupRunner(startupCoordinator);
        _ = startupRunner
            .RunAsync(
                () =>
                {
                    Application.Start(callbackParams =>
                    {
                        _ = callbackParams;
                        var context = new DispatcherQueueSynchronizationContext(
                            DispatcherQueue.GetForCurrentThread());
                        SynchronizationContext.SetSynchronizationContext(context);
                        _ = new App(startupCoordinator);
                    });
                    return Task.CompletedTask;
                },
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return 0;
    }
}
