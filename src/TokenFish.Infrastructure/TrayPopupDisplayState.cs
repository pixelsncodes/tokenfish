namespace TokenFish.Infrastructure;

public sealed record TrayPopupDisplayState(
    PopupApplicationDisplayState ApplicationState,
    string StatusText,
    IReadOnlyList<ProviderCardDisplayState> Providers);
