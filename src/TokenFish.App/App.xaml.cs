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
        _settingsStore = new LocalAppSettingsStore();
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
        PopupWindowPlacement.Configure(_popupWindow);
        _window = _popupWindow;
        _window.Closed += OnWindowClosed;

        var icon = new NativeNotificationAreaIcon(_window, _window.DispatcherQueue);
        var popupShell = new MainWindowPopupShell(_popupWindow, icon);
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
            () => _popupController?.ToggleAsync(CancellationToken.None) ?? Task.CompletedTask,
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
        var window = new OnboardingWindow(new OnboardingFlowController(settings), setup.SettingsSnippet);
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
            await StartRuntimeAsync();
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
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (!_exitRequested)
        {
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

        return Task.CompletedTask;
    }

    private Task OpenSettingsAsync()
    {
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
        return new SettingsWindowShell(new SettingsWindow(
            presenter,
            () => icon.TryGetIconRectangle(out var rectangle) ? rectangle : null));
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

    private void OnPopupSettingsRequested() => _ = OpenSettingsAsync();

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
