namespace TokenFish.Infrastructure;

public interface IPopupShell
{
    bool IsVisible { get; }

    bool IsForeground { get; }

    event Action? Activated;

    event Action? Deactivated;

    event Action? CloseRequested;

    Task ShowAsync(CancellationToken cancellationToken);

    Task HideAsync(CancellationToken cancellationToken);
}
