using Microsoft.UI.Xaml;
using System.Reflection;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public partial class App : Application
{
    private Window? _window;
    private readonly TokenFishApplicationRuntimeHost _runtimeHost;

    public App()
    {
        InitializeComponent();
        _runtimeHost = new TokenFishApplicationRuntimeHost(
            new LocalAppSettingsStore(),
            GetClientVersion());
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Closed += OnWindowClosed;
        _window.Activate();

        _ = StartRuntimeAsync();
    }

    private async Task StartRuntimeAsync()
    {
        await _runtimeHost.StartAsync(CancellationToken.None);
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        await _runtimeHost.StopAsync(CancellationToken.None);
    }

    private static string GetClientVersion()
    {
        var assembly = typeof(App).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ??
            assembly.GetName().Version?.ToString() ??
            "0.0.0";
    }
}
