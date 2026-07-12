namespace TokenFish.Infrastructure;

public enum NotificationAreaCallbackAction
{
    None,
    PrimaryActivate,
    ContextMenu
}

public static class NotificationAreaCallbackDecoder
{
    public const uint WmLButtonUp = 0x0202;
    public const uint WmRButtonUp = 0x0205;
    public const uint WmContextMenu = 0x007B;
    public const uint NinSelect = 0x0400;
    public const uint NinKeySelect = 0x0401;

    public static NotificationAreaCallbackAction DecodeVersion4(
        nint wParam,
        nint lParam,
        uint expectedIconId)
    {
        _ = wParam;

        var callback = LowWord(lParam);
        var iconId = HighWord(lParam);
        if (iconId != (expectedIconId & 0xFFFF))
        {
            return NotificationAreaCallbackAction.None;
        }

        return MapCallback(callback);
    }

    public static NotificationAreaCallbackAction DecodeLegacy(
        nint wParam,
        nint lParam,
        uint expectedIconId)
    {
        if ((uint)wParam != expectedIconId)
        {
            return NotificationAreaCallbackAction.None;
        }

        return MapCallback((uint)lParam);
    }

    private static NotificationAreaCallbackAction MapCallback(uint callback) =>
        callback switch
        {
            WmLButtonUp or NinSelect or NinKeySelect =>
                NotificationAreaCallbackAction.PrimaryActivate,
            WmRButtonUp or WmContextMenu =>
                NotificationAreaCallbackAction.ContextMenu,
            _ => NotificationAreaCallbackAction.None
        };

    private static uint LowWord(nint value) => (uint)value & 0xFFFF;

    private static uint HighWord(nint value) => ((uint)value >> 16) & 0xFFFF;
}
