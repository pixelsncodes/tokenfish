namespace TokenFish.Infrastructure;

public static class PopupWindowStyleMask
{
    public const nint WsDlgFrame = 0x00400000;
    public const nint WsBorder = 0x00800000;
    public const nint WsThickFrame = 0x00040000;
    public const nint WsSysMenu = 0x00080000;
    public const nint WsMinimizeBox = 0x00020000;
    public const nint WsMaximizeBox = 0x00010000;

    private const nint PopupNonClientFlags =
        WsDlgFrame |
        WsBorder |
        WsThickFrame |
        WsSysMenu |
        WsMinimizeBox |
        WsMaximizeBox;

    public static nint RemovePopupNonClientFlags(nint style) => style & ~PopupNonClientFlags;

    public static nint RemovePopupExtendedEdges(nint style) =>
        style & ~(nint)(0x00000001 | 0x00000100 | 0x00000200 | 0x00020000);
}
