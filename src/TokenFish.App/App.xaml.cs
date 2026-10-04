using Microsoft.UI.Xaml;
using System.Reflection;
using TokenFish.App.Platform;
using TokenFish.Core.Models;
using TokenFish.Core.Providers;
using TokenFish.Infrastructure;

namespace TokenFish.App;

public partial class App : Application
{
    private readonly ApplicationInstanceStartupCoordinator _startupCoordinator;
    private Window? _window;
    private MainWindow? _popupWindow;
    private DesktopWidgetWindow? _desktopWidget;
    private SettingsWindow? _settingsWindow;
    private Windows.Graphics.RectInt32? _settingsAnchor;
    private Windows.Graphics.RectInt32? _popupAnchor;
    private AppSettings? _visualSettings;
    private readonly SemaphoreSlim _visualSettingsGate = new(1,1);
    private DispatcherTimer? _widgetTimer;
    private readonly LocalAppSettingsStore _settingsStore;
    private readonly TokenFishApplicationRuntimeHost _runtimeHost;
    private readonly ManualRefreshCommand _manualRefreshCommand;
    private NotificationAreaController? _notificationAreaController;
    private TrayPopupController? _popupController;
    private SettingsWindowCoordinator? _settingsWindowCoordinator;
    private ApplicationRelaunchActivationController? _relaunchActivationController;
    private ApplicationShutdownCoordinator? _shutdownCoordinator;
    private OnboardingWindow? _onboardingWindow;
    private AppSettings? _startupSettings;
    private readonly TrayPopupDisplayStateAdapter _displayStateAdapter = new();
    private readonly IProviderRuntimeSnapshotStore _emptySnapshotStore =
        new InMemoryProviderRuntimeSnapshotStore(TimeProvider.System, TimeSpan.FromMinutes(10));
    private bool _exitRequested;

    public App()
        : this(new ApplicationInstanceStartupCoordinator(new WindowsAppInstanceRegistrar()))
    {
    }

    internal App(ApplicationInstanceStartupCoordinator startupCoordinator)
    {
        ArgumentNullException.ThrowIfNull(startupCoordinator);

        _startupCoordinator = startupCoordinator;
        InitializeComponent();
        var settingsOverride=Environment.GetEnvironmentVariable("TOKENFISH_SETTINGS_PATH");
        _settingsStore = string.IsNullOrWhiteSpace(settingsOverride) ? new LocalAppSettingsStore() : new LocalAppSettingsStore(settingsOverride);
        _runtimeHost = new TokenFishApplicationRuntimeHost(
            _settingsStore,
            GetClientVersion());
        _manualRefreshCommand = new ManualRefreshCommand(_runtimeHost);
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _popupWindow = new MainWindow();
        _popupWindow.RefreshRequested += OnPopupRefreshRequested;
        _popupWindow.SettingsRequested += OnPopupSettingsRequested;
        _popupWindow.WidgetRequested += OnToggleWidgetRequested;
        PopupWindowPlacement.Configure(_popupWindow);
        _window = _popupWindow;
        _window.Closed += OnWindowClosed;

        var icon = new NativeNotificationAreaIcon(_window, _window.DispatcherQueue);
        _desktopWidget = new DesktopWidgetWindow();
        _desktopWidget.OpenRequested += ()=> { _popupAnchor=_desktopWidget.Rectangle; _= _popupController?.ShowAsync(CancellationToken.None); };
        _desktopWidget.SettingsRequested += ()=> { _settingsAnchor=_desktopWidget.Rectangle; _settingsWindowCoordinator?.Open(); };
        _desktopWidget.HideRequested += OnToggleWidgetRequested;
        _desktopWidget.PlacementChanged += (corner,x,y)=>_ = ChangeVisualSettingsAsync(settings=>settings with {DesktopWidgetCorner=corner,DesktopWidgetMonitorX=x,DesktopWidgetMonitorY=y});
        _widgetTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};
        _widgetTimer.Tick+=(_,_)=> { if(_desktopWidget.IsVisible) _=RefreshPopupStateAsync(CancellationToken.None); };
        _widgetTimer.Start();
        var popupShell = new MainWindowPopupShell(_popupWindow, icon, ()=>_popupAnchor,
            ()=> (_settingsWindow is not null && PopupWindowPlacement.IsForeground(_settingsWindow)) ||
                (_desktopWidget.IsVisible && PopupWindowPlacement.IsForeground(_desktopWidget)));
        var popupTimer = new DispatcherPopupUpdateTimer(_window.DispatcherQueue);
        var popupActionQueue = new DispatcherPopupActionQueue(_window.DispatcherQueue);
        _settingsWindowCoordinator = new SettingsWindowCoordinator(
            () => CreateSettingsWindowShell(icon));
        _popupController = new TrayPopupController(
            popupShell,
            popupTimer,
            RefreshPopupStateAsync,
            popupActionQueue);
        _notificationAreaController = new NotificationAreaController(
            icon,
            _runtimeHost,
            _manualRefreshCommand,
            () => { _popupAnchor=null; return _popupController?.ToggleAsync(CancellationToken.None) ?? Task.CompletedTask; },
            OpenSettingsAsync,
            ExitAsync);
        _notificationAreaController.Initialize();
        _relaunchActivationController = new ApplicationRelaunchActivationController(
            _startupCoordinator,
            popupActionQueue,
            cancellationToken => ActivatePrimaryWindowAsync(cancellationToken),
            _runtimeHost.ReportShellFault);
        _relaunchActivationController.Initialize();
        _runtimeHost.StatusChanged += OnRuntimeStatusChanged;
        _runtimeHost.RefreshStatusChanged += OnRefreshStatusChanged;
        _manualRefreshCommand.StateChanged += OnManualRefreshCommandStateChanged;
        _shutdownCoordinator = new ApplicationShutdownCoordinator(
            CleanupForExitAsync,
            CompleteApplicationShutdown,
            _runtimeHost.ReportShellFault);

        _ = InitializeStartupAsync();
    }

    private async Task InitializeStartupAsync()
    {
        var settings = await _settingsStore.LoadAsync(CancellationToken.None);
        _startupSettings = settings;
        _visualSettings = settings;
        _popupWindow?.ApplySettings(settings);
        _desktopWidget?.ApplySettings(settings);

        if (settings.IsOnboardingCompleted)
        {
            await StartRuntimeAsync();
            return;
        }

        ShowOnboarding(settings);
    }

    private Task ActivatePrimaryWindowAsync(CancellationToken cancellationToken)
    {
        if (_onboardingWindow is not null)
        {
            PopupWindowPlacement.BringToForeground(_onboardingWindow);
            return Task.CompletedTask;
        }

        return _popupController?.ShowAsync(cancellationToken) ?? Task.CompletedTask;
    }

    private void ShowOnboarding(AppSettings settings)
    {
        if (_onboardingWindow is not null)
        {
            PopupWindowPlacement.BringToForeground(_onboardingWindow);
            return;
        }

        var setup = ClaudeBridgeSetupGuide.Create(AppContext.BaseDirectory);
        var window = new OnboardingWindow(new OnboardingFlowController(settings), setup.SettingsSnippet,
            claudeWslSnippet: setup.WslSettingsSnippet);
        _onboardingWindow = window;
        window.Deferred += OnOnboardingDeferred;
        window.VerificationRequested += OnOnboardingVerificationRequested;
        window.Completed += OnOnboardingCompleted;
        window.Closed += OnOnboardingClosed;
        window.Activate();
    }

    private void OnOnboardingDeferred() => _ = StartRuntimeAsync();

    private void OnOnboardingVerificationRequested(AppSettings settings) =>
        _ = ApplyOnboardingSettingsAsync(settings);

    private async Task ApplyOnboardingSettingsAsync(AppSettings settings)
    {
        try
        {
            await _settingsStore.SaveAsync(settings, CancellationToken.None);
            await _runtimeHost.ApplySettingsAsync(CancellationToken.None);
            await RefreshPopupStateAsync(CancellationToken.None);
            if (_runtimeHost.Services is { } services && _onboardingWindow is not null)
            {
                _onboardingWindow.ShowVerification(
                    new OnboardingReadinessCoordinator(
                        services.ProviderRuntimeSnapshotStore,
                        services.ProviderRefreshLifecycle),
                    settings);
            }
        }
        catch
        {
            _runtimeHost.ReportShellFault();
        }
    }

    private void OnOnboardingCompleted(AppSettings settings) =>
        _ = CompleteOnboardingAsync(settings);

    private async Task CompleteOnboardingAsync(AppSettings settings)
    {
        try
        {
            await _settingsStore.SaveAsync(settings, CancellationToken.None);
            _onboardingWindow?.CloseAfterCompletion();
        }
        catch
        {
            _runtimeHost.ReportShellFault();
        }
    }

    private void OnOnboardingClosed(object sender, WindowEventArgs args)
    {
        if (sender is OnboardingWindow window)
        {
            window.Deferred -= OnOnboardingDeferred;
            window.VerificationRequested -= OnOnboardingVerificationRequested;
            window.Completed -= OnOnboardingCompleted;
            window.Closed -= OnOnboardingClosed;
        }

        _onboardingWindow = null;
    }

    private async Task StartRuntimeAsync()
    {
        await _runtimeHost.StartAsync(CancellationToken.None);
        await RefreshPopupStateAsync(CancellationToken.None);
        if(Environment.GetCommandLineArgs().Contains("--show",StringComparer.OrdinalIgnoreCase))
            await (_popupController?.ShowAsync(CancellationToken.None) ?? Task.CompletedTask);
        if(Environment.GetCommandLineArgs().Contains("--settings",StringComparer.OrdinalIgnoreCase))
            await OpenSettingsAsync();
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (!_exitRequested)
        {
            _widgetTimer?.Stop();
            _desktopWidget?.Shutdown();
            _relaunchActivationController?.Dispose();
            _popupController?.Dispose();
            _notificationAreaController?.Dispose();
            _manualRefreshCommand.StateChanged -= OnManualRefreshCommandStateChanged;
            _manualRefreshCommand.Dispose();
            if (_popupWindow is not null)
            {
                _popupWindow.RefreshRequested -= OnPopupRefreshRequested;
                _popupWindow.SettingsRequested -= OnPopupSettingsRequested;
            }
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
        var state = _displayStateAdapter.Create(
            settings,
            _runtimeHost.Status,
            _runtimeHost.RefreshStatus,
            _manualRefreshCommand.State,
            snapshotStore);
        _popupWindow.UpdateState(state);
        var appearance = _visualSettings ?? settings;
        _popupWindow.ApplySettings(appearance);
        _desktopWidget?.ApplySettings(appearance);
        _desktopWidget?.UpdateState(state);

        return Task.CompletedTask;
    }

    private Task OpenSettingsAsync()
    {
        _settingsAnchor=null;
        _settingsWindowCoordinator?.Open();
        return Task.CompletedTask;
    }

    private ISettingsWindowShell CreateSettingsWindowShell(NativeNotificationAreaIcon icon)
    {
        var editor = new CodexRuntimeSettingsEditor(_settingsStore);
        var readinessProvider = new ProviderReadinessProvider(
            _runtimeHost.Services?.ProviderRuntimeSnapshotStore);
        var presenter = new CodexRuntimeSettingsPresenter(
            editor,
            readinessProvider,
            _runtimeHost.CurrentSettings);
        var window = new SettingsWindow(
            presenter,
            () => _settingsAnchor ?? (icon.TryGetIconRectangle(out var rectangle) ? rectangle : null),
            _settingsStore,
            _visualSettingsGate);
        _settingsWindow=window;
        window.AppearanceChanged += OnAppearanceSaved;
        window.Closed+=(_,_)=> { window.AppearanceChanged-=OnAppearanceSaved; if(ReferenceEquals(_settingsWindow,window)) _settingsWindow=null; };
        return new SettingsWindowShell(window);
    }

    private void OnRuntimeStatusChanged(ApplicationRuntimeStatus status)
    {
        _ = status;
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _ = RefreshPopupStateAsync(CancellationToken.None);
        });
    }

    private void OnRefreshStatusChanged(ProviderRefreshStatus status)
    {
        _ = status;
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _ = RefreshPopupStateAsync(CancellationToken.None);
        });
    }

    private void OnManualRefreshCommandStateChanged(ManualRefreshCommandState state)
    {
        _ = state;
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _ = RefreshPopupStateAsync(CancellationToken.None);
        });
    }

    private void OnPopupRefreshRequested() =>
        _ = _manualRefreshCommand.RequestAsync(CancellationToken.None);

    private void OnPopupSettingsRequested()
    {
        if(_popupWindow is not null) _settingsAnchor=new(_popupWindow.AppWindow.Position.X,_popupWindow.AppWindow.Position.Y,_popupWindow.AppWindow.Size.Width,_popupWindow.AppWindow.Size.Height);
        _settingsWindowCoordinator?.Open();
    }

    private void OnToggleWidgetRequested()=>_ = ChangeVisualSettingsAsync(settings=>settings with {IsDesktopWidgetVisible=!settings.IsDesktopWidgetVisible});
    private async Task ChangeVisualSettingsAsync(Func<AppSettings,AppSettings> change)
    {
        await _visualSettingsGate.WaitAsync();
        try
        {
            var settings=change(await _settingsStore.LoadAsync(CancellationToken.None));
            await _settingsStore.SaveAsync(settings,CancellationToken.None);
            OnAppearanceSaved(settings);
        }
        catch {_runtimeHost.ReportShellFault();}
        finally {_visualSettingsGate.Release();}
    }
    private void OnAppearanceSaved(AppSettings settings)
    {
        _visualSettings=settings;
        _settingsWindow?.SetVisualControls(settings);
        _popupWindow?.ApplySettings(settings);
        _desktopWidget?.ApplySettings(settings);
        _=RefreshPopupStateAsync(CancellationToken.None);
    }

    private async Task ExitAsync()
    {
        _shutdownCoordinator ??= new ApplicationShutdownCoordinator(
            CleanupForExitAsync,
            CompleteApplicationShutdown,
            _runtimeHost.ReportShellFault);

        await _shutdownCoordinator.ShutdownAsync(CancellationToken.None);
    }

    private async Task CleanupForExitAsync(CancellationToken cancellationToken)
    {
        if (_window is null)
        {
            _settingsWindowCoordinator?.Shutdown();
            await _runtimeHost.StopAsync(cancellationToken);
            _relaunchActivationController?.Dispose();
            _popupController?.Dispose();
            _notificationAreaController?.Dispose();
            _manualRefreshCommand.StateChanged -= OnManualRefreshCommandStateChanged;
            _manualRefreshCommand.Dispose();
            return;
        }

        _exitRequested = true;
        _widgetTimer?.Stop();
        _desktopWidget?.Shutdown();
        _relaunchActivationController?.BeginShutdown();
        _settingsWindowCoordinator?.Shutdown();
        _window.AppWindow.Hide();

        await _runtimeHost.StopAsync(cancellationToken);
        _relaunchActivationController?.Dispose();
        _popupController?.Dispose();
        _notificationAreaController?.Dispose();
        _manualRefreshCommand.StateChanged -= OnManualRefreshCommandStateChanged;
        _manualRefreshCommand.Dispose();
        if (_popupWindow is not null)
        {
            _popupWindow.RefreshRequested -= OnPopupRefreshRequested;
            _popupWindow.SettingsRequested -= OnPopupSettingsRequested;
        }
        _popupWindow?.AllowClose();
        _window.Closed -= OnWindowClosed;
        _window.Close();
    }

    private void CompleteApplicationShutdown() => Exit();

    private static string GetClientVersion()
    {
        var assembly = typeof(App).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ??
            assembly.GetName().Version?.ToString() ??
            "0.0.0";
    }
}
