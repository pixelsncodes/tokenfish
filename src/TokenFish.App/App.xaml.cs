using Microsoft.UI.Xaml;
using System.Reflection;
using TokenFish.App.Platform;
using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public partial class App : Application
{
    private Window? _window;
    private MainWindow? _popupWindow;
    private readonly TokenFishApplicationRuntimeHost _runtimeHost;
    private NotificationAreaController? _notificationAreaController;
    private TrayPopupController? _popupController;
    private readonly TrayPopupDisplayStateAdapter _displayStateAdapter = new();
    private readonly IProviderRuntimeSnapshotStore _emptySnapshotStore =
        new InMemoryProviderRuntimeSnapshotStore(TimeProvider.System, TimeSpan.FromMinutes(10));
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
        _popupWindow = new MainWindow();
        PopupWindowPlacement.Configure(_popupWindow);
        _window = _popupWindow;
        _window.Closed += OnWindowClosed;

        var icon = new NativeNotificationAreaIcon(_window, _window.DispatcherQueue);
        var popupShell = new MainWindowPopupShell(_popupWindow, icon);
        var popupTimer = new DispatcherPopupUpdateTimer(_window.DispatcherQueue);
        _popupController = new TrayPopupController(
            popupShell,
            popupTimer,
            RefreshPopupStateAsync);
        _notificationAreaController = new NotificationAreaController(
            icon,
            _runtimeHost,
            () => _popupController?.ToggleAsync(CancellationToken.None) ?? Task.CompletedTask,
            ExitAsync);
        _notificationAreaController.Initialize();
        _runtimeHost.StatusChanged += OnRuntimeStatusChanged;

        _ = StartRuntimeAsync();
    }

    private async Task StartRuntimeAsync()
    {
        await _runtimeHost.StartAsync(CancellationToken.None);
        await RefreshPopupStateAsync(CancellationToken.None);
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (!_exitRequested)
        {
            _popupController?.Dispose();
            _notificationAreaController?.Dispose();
        }

        await _runtimeHost.StopAsync(CancellationToken.None);
    }

    private Task RefreshPopupStateAsync(CancellationToken cancellationToken)
    {
        if (_popupWindow is null)
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var settings = _runtimeHost.CurrentSettings ?? new AppSettings();
        var snapshotStore = _runtimeHost.Services?.ProviderRuntimeSnapshotStore ?? _emptySnapshotStore;
        var state = _displayStateAdapter.Create(settings, _runtimeHost.Status, snapshotStore);
        _popupWindow.UpdateState(state);

        return Task.CompletedTask;
    }

    private void OnRuntimeStatusChanged(ApplicationRuntimeStatus status)
    {
        _ = status;
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _ = RefreshPopupStateAsync(CancellationToken.None);
        });
    }

    private async Task ExitAsync()
    {
        if (_window is null)
        {
            await _runtimeHost.StopAsync(CancellationToken.None);
            _popupController?.Dispose();
            _notificationAreaController?.Dispose();
            return;
        }

        _exitRequested = true;
        _window.AppWindow.Hide();

        await _runtimeHost.StopAsync(CancellationToken.None);
        _popupController?.Dispose();
        _notificationAreaController?.Dispose();
        _popupWindow?.AllowClose();
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
