namespace TokenFish.Infrastructure;

public enum ApplicationRuntimeState
{
    Starting,
    Running,
    Refreshing,
    Faulted,
    Stopping,
    Stopped
}
