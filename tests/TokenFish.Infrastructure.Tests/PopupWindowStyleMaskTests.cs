using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class PopupWindowStyleMaskTests
{
    [Fact]
    public void RemovesExtendedEdgesWithoutLosingToolWindowOrTopmostFlags()
    {
        const nint preserved = 0x00000080 | 0x00000008;
        const nint edges = 0x00000001 | 0x00000100 | 0x00000200 | 0x00020000;
        Assert.Equal(preserved, PopupWindowStyleMask.RemovePopupExtendedEdges(preserved | edges));
        Assert.Equal(preserved, PopupWindowStyleMask.RemovePopupExtendedEdges(preserved));
    }
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
