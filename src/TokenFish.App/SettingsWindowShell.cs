using Microsoft.UI.Xaml;
using TokenFish.App.Platform;
using TokenFish.Infrastructure;

namespace TokenFish.App;

internal sealed class SettingsWindowShell : ISettingsWindowShell
{
    private readonly SettingsWindow _window;

    public SettingsWindowShell(SettingsWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
        _window.Closed += OnClosed;
    }

    public event Action<ISettingsWindowShell>? Closed;

    public void Show() => _window.Activate();

    public void Activate()
    {
        _window.RepositionForInvocation();
        PopupWindowPlacement.BringToForeground(_window);
    }

    public void Close()
    {
        _window.Closed -= OnClosed;
        _window.CaptureCurrentPlacement();
        _window.Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _ = sender;
        _ = args;
        _window.Closed -= OnClosed;
        _window.CaptureCurrentPlacement();
        Closed?.Invoke(this);
    }
}
