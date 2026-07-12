namespace TokenFish.Infrastructure;

public sealed record ApplicationRuntimeStatus(
    ApplicationRuntimeState State,
    ApplicationRuntimeIssue Issue)
{
    public static ApplicationRuntimeStatus Stopped { get; } =
        new(ApplicationRuntimeState.Stopped, ApplicationRuntimeIssue.None);

    public static ApplicationRuntimeStatus Starting { get; } =
        new(ApplicationRuntimeState.Starting, ApplicationRuntimeIssue.None);

    public static ApplicationRuntimeStatus Running { get; } =
        new(ApplicationRuntimeState.Running, ApplicationRuntimeIssue.None);

    public static ApplicationRuntimeStatus Refreshing { get; } =
        new(ApplicationRuntimeState.Refreshing, ApplicationRuntimeIssue.None);

    public static ApplicationRuntimeStatus Stopping { get; } =
        new(ApplicationRuntimeState.Stopping, ApplicationRuntimeIssue.None);

    public static ApplicationRuntimeStatus StartupFaulted { get; } =
        new(ApplicationRuntimeState.Faulted, ApplicationRuntimeIssue.StartupFailed);

    public static ApplicationRuntimeStatus RefreshFaulted { get; } =
        new(ApplicationRuntimeState.Faulted, ApplicationRuntimeIssue.RefreshFailed);

    public static ApplicationRuntimeStatus ShutdownFaulted { get; } =
        new(ApplicationRuntimeState.Faulted, ApplicationRuntimeIssue.ShutdownFailed);
}
