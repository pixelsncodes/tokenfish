using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TokenFish.App.Platform;
using TokenFish.Infrastructure;

namespace TokenFish.App;

internal sealed class MainWindowPopupShell : IPopupShell
{
    private readonly MainWindow _window;
    private readonly NativeNotificationAreaIcon _icon;

    public MainWindowPopupShell(MainWindow window, NativeNotificationAreaIcon icon)
    {
        _window = window;
        _icon = icon;
        _window.PopupDeactivated += OnPopupDeactivated;
        _window.PopupCloseRequested += OnPopupCloseRequested;
    }

    public bool IsVisible { get; private set; }

    public event Action? Deactivated;

    public event Action? CloseRequested;

    public Task ShowAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var iconRectangle = _icon.TryGetIconRectangle(out var rectangle)
            ? rectangle
            : (Windows.Graphics.RectInt32?)null;
        PopupWindowPlacement.PositionBesideIcon(_window, iconRectangle);
        PopupWindowPlacement.BringToForeground(_window);
        IsVisible = true;
        return Task.CompletedTask;
    }

    public Task HideAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _window.AppWindow.Hide();
        IsVisible = false;
        return Task.CompletedTask;
    }

    private void OnPopupDeactivated() => Deactivated?.Invoke();

    private void OnPopupCloseRequested() => CloseRequested?.Invoke();
}

internal sealed class DispatcherPopupUpdateTimer : IPopupUpdateTimer
{
    private readonly DispatcherTimer _timer;

    public DispatcherPopupUpdateTimer(DispatcherQueue dispatcherQueue)
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => Tick?.Invoke();
    }

    public event Action? Tick;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
