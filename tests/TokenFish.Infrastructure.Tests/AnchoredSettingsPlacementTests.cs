using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class AnchoredSettingsPlacementTests
{
    [Theory]
    [InlineData(20, 20, 312, 0)]
    [InlineData(1620, 20, 1088, 0)]
    [InlineData(20, 900, 312, 570)]
    [InlineData(1620, 900, 1088, 570)]
    public void PlacementFollowsEachCornerAndClamps(int x, int y, int expectedX, int expectedY)
    {
        var area = new SettingsMonitorWorkArea(0, 0, 1920, 1040);
        var result = SettingsWindowPositionCalculator.CalculateBeside(area, new(520, 430), new(x, y, 280, 100));
        Assert.Equal(new SettingsWindowPhysicalRect(expectedX, expectedY, 520, 430), result.Rectangle);
        Assert.True(area.Contains(result.Rectangle));
    }

    [Fact]
    public void NegativeMonitorAndScaledGapRemainOnInvokingMonitor()
    {
        var area = new SettingsMonitorWorkArea(-1920, -200, 1920, 1040);
        var result = SettingsWindowPositionCalculator.CalculateBeside(area, new(780, 645), new(-320, 680, 280, 100), 18);
        Assert.Equal(new SettingsWindowPhysicalRect(-1118, 135, 780, 645), result.Rectangle);
        Assert.True(area.Contains(result.Rectangle));
    }

    [Fact]
    public void NarrowWorkAreaUsesVerticalPlacement()
    {
        var area = new SettingsMonitorWorkArea(0, 0, 800, 1000);
        var result = SettingsWindowPositionCalculator.CalculateBeside(area, new(680, 400), new(260, 850, 280, 100));
        Assert.Equal(new SettingsWindowPhysicalRect(0, 438, 680, 400), result.Rectangle);
        Assert.True(area.Contains(result.Rectangle));
    }
}
