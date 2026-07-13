namespace TokenFish.Infrastructure;

public sealed record PopupWindowPresenterConfiguration(
    bool HasBorder,
    bool HasTitleBar,
    bool IsResizable,
    bool IsMaximizable,
    bool IsMinimizable)
{
    public static PopupWindowPresenterConfiguration TrayPopup { get; } = new(
        HasBorder: false,
        HasTitleBar: false,
        IsResizable: false,
        IsMaximizable: false,
        IsMinimizable: false);
}
