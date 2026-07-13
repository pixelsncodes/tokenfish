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
        _window.PopupActivated += OnPopupActivated;
        _window.PopupDeactivated += OnPopupDeactivated;
        _window.PopupCloseRequested += OnPopupCloseRequested;
        _window.ContentSizeInvalidated += OnContentSizeInvalidated;
    }

    public bool IsVisible { get; private set; }

    public bool IsForeground => PopupWindowPlacement.IsForeground(_window);

    public event Action? Activated;

    public event Action? Deactivated;

    public event Action? CloseRequested;

    public Task ShowAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var iconRectangle = _icon.TryGetIconRectangle(out var rectangle)
            ? rectangle
            : (Windows.Graphics.RectInt32?)null;
        PositionBesideIcon(iconRectangle);
        _window.AppWindow.Show();
        PopupWindowPlacement.EnsureBorderlessAfterShowing(_window);
        IsVisible = true;
        PopupWindowPlacement.BringToForeground(_window);
        return Task.CompletedTask;
    }

    public Task HideAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _window.AppWindow.Hide();
        IsVisible = false;
        return Task.CompletedTask;
    }

    private void OnPopupActivated() => Activated?.Invoke();

    private void OnPopupDeactivated() => Deactivated?.Invoke();

    private void OnPopupCloseRequested() => CloseRequested?.Invoke();

    private void OnContentSizeInvalidated()
    {
        if (!IsVisible)
        {
            return;
        }

        var iconRectangle = _icon.TryGetIconRectangle(out var rectangle)
            ? rectangle
            : (Windows.Graphics.RectInt32?)null;
        PositionBesideIcon(iconRectangle);
    }

    private void PositionBesideIcon(Windows.Graphics.RectInt32? iconRectangle)
    {
        PopupWindowPlacement.PositionBesideIcon(
            _window,
            iconRectangle,
            _window.MeasurePreferredHeightEffectivePixels());
    }
}

internal sealed class DispatcherPopupActionQueue : IPopupActionQueue
{
    private readonly DispatcherQueue _dispatcherQueue;

    public DispatcherPopupActionQueue(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void Enqueue(Action action)
    {
        _dispatcherQueue.TryEnqueue(() => action());
    }
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
