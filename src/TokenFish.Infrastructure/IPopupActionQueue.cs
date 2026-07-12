namespace TokenFish.Infrastructure;

public interface IPopupActionQueue
{
    void Enqueue(Action action);
}
