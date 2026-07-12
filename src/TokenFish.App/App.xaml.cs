using Microsoft.UI.Xaml;
using System.Reflection;
using TokenFish.App.Platform;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public partial class App : Application
{
    private Window? _window;
    private readonly TokenFishApplicationRuntimeHost _runtimeHost;
    private NotificationAreaController? _notificationAreaController;
    private bool _windowVisible;
    private bool _exitRequested;

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
        _windowVisible = true;

        var icon = new NativeNotificationAreaIcon(_window, _window.DispatcherQueue);
        _notificationAreaController = new NotificationAreaController(
            icon,
            _runtimeHost,
            ToggleWindowAsync,
            ExitAsync);
        _notificationAreaController.Initialize();

        _ = StartRuntimeAsync();
    }

    private async Task StartRuntimeAsync()
    {
        await _runtimeHost.StartAsync(CancellationToken.None);
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (!_exitRequested)
        {
            _notificationAreaController?.Dispose();
        }

        await _runtimeHost.StopAsync(CancellationToken.None);
    }

    private Task ToggleWindowAsync()
    {
        if (_window is null)
        {
            return Task.CompletedTask;
        }

        if (_windowVisible)
        {
            _window.AppWindow.Hide();
            _windowVisible = false;
        }
        else
        {
            _window.Activate();
            _windowVisible = true;
        }

        return Task.CompletedTask;
    }

    private async Task ExitAsync()
    {
        if (_window is null)
        {
            await _runtimeHost.StopAsync(CancellationToken.None);
            _notificationAreaController?.Dispose();
            return;
        }

        _exitRequested = true;
        _window.AppWindow.Hide();
        _windowVisible = false;

        await _runtimeHost.StopAsync(CancellationToken.None);
        _notificationAreaController?.Dispose();
        _window.Close();
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
