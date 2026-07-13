using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class PopupWindowStyleMaskTests
{
    [Fact]
    public void RemovesOnlyPopupNonClientFrameFlags()
    {
        const nint unrelatedFlags = 0x14000000;
        var style = unrelatedFlags |
            PopupWindowStyleMask.WsDlgFrame |
            PopupWindowStyleMask.WsBorder |
            PopupWindowStyleMask.WsThickFrame |
            PopupWindowStyleMask.WsSysMenu |
            PopupWindowStyleMask.WsMinimizeBox |
            PopupWindowStyleMask.WsMaximizeBox;

        Assert.Equal(unrelatedFlags, PopupWindowStyleMask.RemovePopupNonClientFlags(style));
    }

    [Fact]
    public void PreservesUnrelatedStyleFlags()
    {
        const nint style = 0x14000000;

        Assert.Equal(style, PopupWindowStyleMask.RemovePopupNonClientFlags(style));
    }

    [Fact]
    public void LeavesAlreadyCleanStylesUnchanged()
    {
        const nint style = 0;

        Assert.Equal(style, PopupWindowStyleMask.RemovePopupNonClientFlags(style));
    }
}
