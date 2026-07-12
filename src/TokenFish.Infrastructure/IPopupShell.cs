namespace TokenFish.Infrastructure;

public interface IPopupShell
{
    bool IsVisible { get; }

    event Action? Deactivated;

    event Action? CloseRequested;

    Task ShowAsync(CancellationToken cancellationToken);

    Task HideAsync(CancellationToken cancellationToken);
}
