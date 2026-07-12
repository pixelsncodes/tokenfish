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

        var startupRunner = new ApplicationInstanceStartupRunner(
            new ApplicationInstanceStartupCoordinator(new WindowsAppInstanceRegistrar()));
        _ = startupRunner
            .RunAsync(
                () =>
                {
                    Application.Start(_ => new App());
                    return Task.CompletedTask;
                },
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return 0;
    }
}
