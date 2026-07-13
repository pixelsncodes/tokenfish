namespace TokenFish.Infrastructure;

public sealed record ManualRefreshCommandState(
    bool IsEnabled,
    string Label,
    string StatusText)
{
    public static ManualRefreshCommandState Available { get; } = new(
        IsEnabled: true,
        Label: "Refresh now",
        StatusText: string.Empty);

    public static ManualRefreshCommandState Refreshing { get; } = new(
        IsEnabled: false,
        Label: "Refreshing…",
        StatusText: "Refreshing usage");
}
