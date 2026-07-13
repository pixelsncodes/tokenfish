namespace TokenFish.Infrastructure;

public sealed record TrayPopupDisplayState(
    PopupApplicationDisplayState ApplicationState,
    string StatusText,
    IReadOnlyList<ProviderCardDisplayState> Providers,
    ManualRefreshCommandState RefreshCommandState)
{
    public TrayPopupDisplayState(
        PopupApplicationDisplayState applicationState,
        string statusText,
        IReadOnlyList<ProviderCardDisplayState> providers)
        : this(
            applicationState,
            statusText,
            providers,
            ManualRefreshCommandState.Available)
    {
    }
}
