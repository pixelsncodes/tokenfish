using TokenFish.Core.Models;

namespace TokenFish.Infrastructure;

public static class DesktopWidgetLayoutCalculator
{
    public static SettingsWindowPhysicalRect Calculate(SettingsMonitorWorkArea workArea,
        SettingsWindowPhysicalSize desiredSize, DesktopWidgetCorner corner, int margin = 16)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0 || desiredSize.Width <= 0 || desiredSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(desiredSize));
        ArgumentOutOfRangeException.ThrowIfNegative(margin);
        if (!Enum.IsDefined(corner)) throw new ArgumentOutOfRangeException(nameof(corner));
        var width = Math.Min(desiredSize.Width, workArea.Width);
        var height = Math.Min(desiredSize.Height, workArea.Height);
        var insetX = Math.Min(margin, (workArea.Width-width)/2);
        var insetY = Math.Min(margin, (workArea.Height-height)/2);
        var left = corner is DesktopWidgetCorner.BottomLeft or DesktopWidgetCorner.TopLeft;
        var top = corner is DesktopWidgetCorner.TopLeft or DesktopWidgetCorner.TopRight;
        return new(left ? workArea.X+insetX : workArea.Right-width-insetX,
            top ? workArea.Y+insetY : workArea.Bottom-height-insetY, width, height);
    }

    public static DesktopWidgetCorner ClosestCorner(SettingsWindowPhysicalRect rectangle, SettingsMonitorWorkArea area)
    {
        var left = rectangle.Center.X < area.X + area.Width/2;
        var top = rectangle.Center.Y < area.Y + area.Height/2;
        return (left,top) switch
        {
            (true,true) => DesktopWidgetCorner.TopLeft,
            (false,true) => DesktopWidgetCorner.TopRight,
            (true,false) => DesktopWidgetCorner.BottomLeft,
            _ => DesktopWidgetCorner.BottomRight
        };
    }
}
