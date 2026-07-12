namespace TokenFish.Infrastructure;

public interface INotificationAreaIcon : IDisposable
{
    event Action<NotificationAreaCommand>? CommandRequested;

    event Action? ShellFaulted;

    void Initialize();

    void Restore();
}
