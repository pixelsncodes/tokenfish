using TokenFish.Core.Models;

namespace TokenFish.Infrastructure.Tests;

public sealed class DesktopWidgetLayoutCalculatorTests
{
    [Theory]
    [InlineData(DesktopWidgetCorner.BottomRight,1622,908)]
    [InlineData(DesktopWidgetCorner.BottomLeft,16,908)]
    [InlineData(DesktopWidgetCorner.TopRight,1622,16)]
    [InlineData(DesktopWidgetCorner.TopLeft,16,16)]
    public void WidgetSnapsToEachCorner(DesktopWidgetCorner corner,int x,int y)
    {
        var area = new SettingsMonitorWorkArea(0,0,1920,1040);
        var result = DesktopWidgetLayoutCalculator.Calculate(area,new(282,116),corner);
        Assert.Equal(new SettingsWindowPhysicalRect(x,y,282,116),result);
        Assert.Equal(corner,DesktopWidgetLayoutCalculator.ClosestCorner(result,area));
    }

    [Theory]
    [InlineData(1.0)] [InlineData(1.25)] [InlineData(1.5)] [InlineData(2.0)]
    public void NegativeMonitorAndScalingStayWithinWorkArea(double scale)
    {
        var area = new SettingsMonitorWorkArea(-2560,-200,2560,1400);
        foreach (var corner in Enum.GetValues<DesktopWidgetCorner>())
        {
            var result = DesktopWidgetLayoutCalculator.Calculate(area,new((int)(282*scale),(int)(116*scale)),corner,(int)(16*scale));
            Assert.True(area.Contains(result));
            Assert.Equal(corner,DesktopWidgetLayoutCalculator.ClosestCorner(result,area));
        }
    }

    [Fact]
    public void RemovedMonitorFallbackAndSmallWorkAreaKeepWidgetVisible()
    {
        var area = new SettingsMonitorWorkArea(50,100,240,100);
        var result = DesktopWidgetLayoutCalculator.Calculate(area,new(282,116),DesktopWidgetCorner.BottomRight);
        Assert.Equal(new SettingsWindowPhysicalRect(50,100,240,100),result);
    }
}
