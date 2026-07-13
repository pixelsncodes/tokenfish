namespace TokenFish.Infrastructure;

public sealed class SettingsWindowCoordinator
{
    private readonly Func<ISettingsWindowShell> _createWindow;
    private readonly object _sync = new();

    private ISettingsWindowShell? _window;
    private bool _shutdownStarted;

    public SettingsWindowCoordinator(Func<ISettingsWindowShell> createWindow)
    {
        ArgumentNullException.ThrowIfNull(createWindow);

        _createWindow = createWindow;
    }

    public void Open()
    {
        ISettingsWindowShell window;
        var created = false;

        lock (_sync)
        {
            if (_shutdownStarted)
            {
                return;
            }

            if (_window is null)
            {
                window = _createWindow();
                window.Closed += OnWindowClosed;
                _window = window;
                created = true;
            }
            else
            {
                window = _window;
            }
        }

        if (created)
        {
            window.Show();
        }

        window.Activate();
    }

    public void Shutdown()
    {
        ISettingsWindowShell? window;

        lock (_sync)
        {
            if (_shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
            window = _window;
            _window = null;
        }

        if (window is null)
        {
            return;
        }

        window.Closed -= OnWindowClosed;
        window.Close();
    }

    private void OnWindowClosed(ISettingsWindowShell closedWindow)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_window, closedWindow))
            {
                return;
            }

            _window.Closed -= OnWindowClosed;
            _window = null;
        }
    }
}

public interface ISettingsWindowShell
{
    event Action<ISettingsWindowShell>? Closed;

    void Show();

    void Activate();

    void Close();
}
