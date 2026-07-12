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

        return MapVersion4Callback(callback);
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

        return MapLegacyCallback((uint)lParam);
    }

    private static NotificationAreaCallbackAction MapVersion4Callback(uint callback) =>
        callback switch
        {
            NinSelect or NinKeySelect =>
                NotificationAreaCallbackAction.PrimaryActivate,
            WmContextMenu =>
                NotificationAreaCallbackAction.ContextMenu,
            _ => NotificationAreaCallbackAction.None
        };

    private static NotificationAreaCallbackAction MapLegacyCallback(uint callback) =>
        callback switch
        {
            WmLButtonUp =>
                NotificationAreaCallbackAction.PrimaryActivate,
            WmRButtonUp =>
                NotificationAreaCallbackAction.ContextMenu,
            _ => NotificationAreaCallbackAction.None
        };

    private static uint LowWord(nint value) => (uint)value & 0xFFFF;

    private static uint HighWord(nint value) => ((uint)value >> 16) & 0xFFFF;
}
