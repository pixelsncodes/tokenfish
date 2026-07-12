namespace TokenFish.Infrastructure;

public interface IPopupUpdateTimer
{
    event Action? Tick;

    void Start();

    void Stop();
}
