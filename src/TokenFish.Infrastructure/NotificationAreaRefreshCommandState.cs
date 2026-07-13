namespace TokenFish.Infrastructure;

public sealed record NotificationAreaRefreshCommandState(bool IsEnabled, string Label)
{
    public static NotificationAreaRefreshCommandState Available { get; } = new(
        IsEnabled: true,
        Label: "Refresh");

    public static NotificationAreaRefreshCommandState Updating { get; } = new(
        IsEnabled: false,
        Label: "Refresh (updating)");
}
