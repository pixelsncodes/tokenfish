using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class SettingsWindowLayoutCalculatorTests
{
    [Theory]
    [InlineData(1.0, 680)]
    [InlineData(1.25, 850)]
    [InlineData(1.5, 1020)]
    [InlineData(2.0, 1360)]
    public void EffectiveWidthConvertsToPhysicalPixels(double scale, int expected)
    {
        var physical = SettingsWindowLayoutCalculator.EffectiveToPhysicalPixels(
            SettingsWindowLayoutCalculator.WidthEffectivePixels,
            scale);

        Assert.Equal(expected, physical);
    }

    [Theory]
    [InlineData(1.0, 390, 8, 40, 688, 432)]
    [InlineData(1.25, 390, 10, 50, 860, 540)]
    [InlineData(1.5, 390, 12, 60, 1032, 648)]
    [InlineData(2.0, 390, 16, 80, 1376, 864)]
    public void OuterSizeAddsNonClientFrameToMeasuredClientArea(
        double scale,
        double measuredClientHeight,
        int nonClientWidth,
        int nonClientHeight,
        int expectedWidth,
        int expectedHeight)
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeight,
            scale,
            nonClientWidth,
            nonClientHeight);

        Assert.Equal(expectedWidth, layout.Size.Width);
        Assert.Equal(expectedHeight, layout.Size.Height);
        Assert.Equal(392, layout.ClientHeightEffectivePixels);
    }

    [Fact]
    public void FractionalMeasuredHeightRoundsUpBeforeScaling()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 390.1,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 0,
            nonClientHeightPhysicalPixels: 0);

        Assert.Equal(492, layout.Size.Height);
        Assert.Equal(393, layout.ClientHeightEffectivePixels);
    }

    [Fact]
    public void TallContentFitsWhenWorkAreaCanContainIt()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 800,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50);

        Assert.Equal(1053, layout.Size.Height);
        Assert.Equal(802, layout.ClientHeightEffectivePixels);
        Assert.False(layout.RequiresVerticalScroll);
    }

    [Theory]
    [InlineData(650, "Codex")]
    [InlineData(610, "Claude")]
    [InlineData(700, "Codex and Claude")]
    public void ProviderModeRequiredHeightFitsWithoutScrolling(
        double measuredClientHeight,
        string providerMode)
    {
        _ = providerMode;

        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeight,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50,
            maximumWindowHeightPhysicalPixels: 1200);

        Assert.Equal(measuredClientHeight + 2, layout.ClientHeightEffectivePixels);
        Assert.False(layout.RequiresVerticalScroll);
    }

    [Fact]
    public void FooterAndRootPaddingAreIncludedInMeasuredContentHeight()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 705,
            rasterizationScale: 1,
            nonClientWidthPhysicalPixels: 8,
            nonClientHeightPhysicalPixels: 40,
            maximumWindowHeightPhysicalPixels: 900);

        Assert.Equal(707, layout.ClientHeightEffectivePixels);
        Assert.Equal(747, layout.Size.Height);
        Assert.False(layout.RequiresVerticalScroll);
    }

    [Fact]
    public void PendingRestartPanelIsIncludedInRequiredHeight()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 780,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50,
            maximumWindowHeightPhysicalPixels: 1300);

        Assert.Equal(782, layout.ClientHeightEffectivePixels);
        Assert.Equal(1028, layout.Size.Height);
        Assert.False(layout.RequiresVerticalScroll);
    }

    [Theory]
    [InlineData(1.0, 688, 692)]
    [InlineData(1.25, 860, 865)]
    [InlineData(1.5, 1032, 1038)]
    [InlineData(2.0, 1376, 1384)]
    public void DesiredHeightScalesFromEffectivePixels(
        double scale,
        int expectedWidth,
        int expectedHeight)
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 650,
            rasterizationScale: scale,
            nonClientWidthPhysicalPixels: (int)(8 * scale),
            nonClientHeightPhysicalPixels: (int)(40 * scale),
            maximumWindowHeightPhysicalPixels: 2000);

        Assert.Equal(expectedWidth, layout.Size.Width);
        Assert.Equal(expectedHeight, layout.Size.Height);
        Assert.False(layout.RequiresVerticalScroll);
    }

    [Fact]
    public void SmallWorkAreaFallsBackToScrolling()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 650,
            rasterizationScale: 1,
            nonClientWidthPhysicalPixels: 8,
            nonClientHeightPhysicalPixels: 40,
            maximumWindowHeightPhysicalPixels: 500);

        Assert.Equal(460, layout.ClientHeightEffectivePixels);
        Assert.Equal(500, layout.Size.Height);
        Assert.True(layout.RequiresVerticalScroll);
    }

    [Fact]
    public void FractionalDipOverflowGetsBoundedRoundingAllowance()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 720.8,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50,
            maximumWindowHeightPhysicalPixels: 960);

        Assert.Equal(723, layout.ClientHeightEffectivePixels);
        Assert.Equal(954, layout.Size.Height);
        Assert.False(layout.RequiresVerticalScroll);
    }

    [Fact]
    public void MaximumHeightRespectsMonitorWorkArea()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 700,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50,
            maximumWindowHeightPhysicalPixels: 850);

        Assert.Equal(640, layout.ClientHeightEffectivePixels);
        Assert.Equal(850, layout.Size.Height);
        Assert.True(layout.RequiresVerticalScroll);
    }

    [Fact]
    public void ProviderSelectionHeightChangesAreDeterministic()
    {
        var claudeOnly = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 610,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50,
            maximumWindowHeightPhysicalPixels: 1200);
        var combined = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 700,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50,
            maximumWindowHeightPhysicalPixels: 1200);

        Assert.Equal(612, claudeOnly.ClientHeightEffectivePixels);
        Assert.Equal(702, combined.ClientHeightEffectivePixels);
        Assert.False(claudeOnly.RequiresVerticalScroll);
        Assert.False(combined.RequiresVerticalScroll);
    }

    [Fact]
    public void RepeatedLayoutCalculationDoesNotGrowCumulatively()
    {
        var first = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 650,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50,
            maximumWindowHeightPhysicalPixels: 1200);
        var second = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 650,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50,
            maximumWindowHeightPhysicalPixels: 1200);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SettingsWindowCentersOnPrimaryMonitorAtOrigin()
    {
        var placement = SettingsWindowPositionCalculator.Calculate(
            [new SettingsMonitorWorkArea(0, 0, 1920, 1040, IsPrimary: true)],
            new SettingsWindowPhysicalSize(520, 430),
            new SettingsPhysicalPoint(200, 200),
            lastWindowRectangle: null);

        Assert.Equal(new SettingsWindowPhysicalRect(700, 305, 520, 430), placement.Rectangle);
        Assert.False(placement.ReusedLastPosition);
    }

    [Fact]
    public void SettingsWindowCentersOnSecondaryMonitorToRight()
    {
        var placement = SettingsWindowPositionCalculator.Calculate(
            [
                new SettingsMonitorWorkArea(0, 0, 1920, 1040, IsPrimary: true),
                new SettingsMonitorWorkArea(1920, 0, 1920, 1040)
            ],
            new SettingsWindowPhysicalSize(520, 430),
            new SettingsPhysicalPoint(2500, 300),
            lastWindowRectangle: null);

        Assert.Equal(new SettingsWindowPhysicalRect(2620, 305, 520, 430), placement.Rectangle);
    }

    [Fact]
    public void SettingsWindowCentersOnSecondaryMonitorToLeftWithNegativeCoordinates()
    {
        var placement = SettingsWindowPositionCalculator.Calculate(
            [
                new SettingsMonitorWorkArea(-1920, 0, 1920, 1040),
                new SettingsMonitorWorkArea(0, 0, 1920, 1040, IsPrimary: true)
            ],
            new SettingsWindowPhysicalSize(520, 430),
            new SettingsPhysicalPoint(-800, 300),
            lastWindowRectangle: null);

        Assert.Equal(new SettingsWindowPhysicalRect(-1220, 305, 520, 430), placement.Rectangle);
    }

    [Theory]
    [InlineData(0, -1080, 700, -775)]
    [InlineData(0, 1080, 700, 1385)]
    public void SettingsWindowCentersOnMonitorAboveOrBelowPrimary(
        int monitorX,
        int monitorY,
        int expectedX,
        int expectedY)
    {
        var placement = SettingsWindowPositionCalculator.Calculate(
            [
                new SettingsMonitorWorkArea(0, 0, 1920, 1040, IsPrimary: true),
                new SettingsMonitorWorkArea(monitorX, monitorY, 1920, 1040)
            ],
            new SettingsWindowPhysicalSize(520, 430),
            new SettingsPhysicalPoint(monitorX + 100, monitorY + 100),
            lastWindowRectangle: null);

        Assert.Equal(new SettingsWindowPhysicalRect(expectedX, expectedY, 520, 430), placement.Rectangle);
    }

    [Theory]
    [InlineData(1.25, 850, 538, 775, 381)]
    [InlineData(1.5, 1020, 645, 930, 457)]
    [InlineData(2.0, 1360, 860, 1240, 610)]
    public void SettingsWindowCentersUsingPhysicalPixelsAtEffectiveScaling(
        double scale,
        int expectedWidth,
        int expectedHeight,
        int expectedX,
        int expectedY)
    {
        var width = SettingsWindowLayoutCalculator.EffectiveToPhysicalPixels(
            SettingsWindowLayoutCalculator.WidthEffectivePixels,
            scale);
        var height = SettingsWindowLayoutCalculator.EffectiveToPhysicalPixels(430, scale);
        var placement = SettingsWindowPositionCalculator.Calculate(
            [
                new SettingsMonitorWorkArea(
                    0,
                    0,
                    (int)(1920 * scale),
                    (int)(1040 * scale),
                    IsPrimary: true)
            ],
            new SettingsWindowPhysicalSize(width, height),
            new SettingsPhysicalPoint(20, 20),
            lastWindowRectangle: null);

        Assert.Equal(new SettingsWindowPhysicalRect(expectedX, expectedY, expectedWidth, expectedHeight), placement.Rectangle);
    }

    [Fact]
    public void SettingsWindowCentersWithinWorkAreaReducedByTaskbar()
    {
        var placement = SettingsWindowPositionCalculator.Calculate(
            [new SettingsMonitorWorkArea(0, 0, 1920, 1000, IsPrimary: true)],
            new SettingsWindowPhysicalSize(520, 430),
            new SettingsPhysicalPoint(100, 100),
            lastWindowRectangle: null);

        Assert.Equal(new SettingsWindowPhysicalRect(700, 285, 520, 430), placement.Rectangle);
    }

    [Fact]
    public void SettingsWindowLargerThanWorkAreaStartsAtWorkAreaOrigin()
    {
        var placement = SettingsWindowPositionCalculator.Calculate(
            [new SettingsMonitorWorkArea(100, 50, 800, 500, IsPrimary: true)],
            new SettingsWindowPhysicalSize(900, 600),
            new SettingsPhysicalPoint(200, 100),
            lastWindowRectangle: null);

        Assert.Equal(new SettingsWindowPhysicalRect(100, 50, 900, 600), placement.Rectangle);
    }

    [Theory]
    [InlineData(50, 100, 100, 100)]
    [InlineData(900, 100, 580, 100)]
    [InlineData(100, 20, 100, 50)]
    [InlineData(100, 650, 100, 470)]
    public void SettingsWindowClampKeepsCompleteRectangleInWorkAreaWhenItFits(
        int requestedX,
        int requestedY,
        int expectedX,
        int expectedY)
    {
        var clamped = SettingsWindowPositionCalculator.ClampToWorkArea(
            new SettingsWindowPhysicalRect(requestedX, requestedY, 320, 180),
            new SettingsMonitorWorkArea(100, 50, 800, 600, IsPrimary: true));

        Assert.Equal(new SettingsWindowPhysicalRect(expectedX, expectedY, 320, 180), clamped);
    }

    [Fact]
    public void SettingsWindowReusesLastValidPosition()
    {
        var lastRectangle = new SettingsWindowPhysicalRect(2100, 140, 520, 430);
        var placement = SettingsWindowPositionCalculator.Calculate(
            [
                new SettingsMonitorWorkArea(0, 0, 1920, 1040, IsPrimary: true),
                new SettingsMonitorWorkArea(1920, 0, 1920, 1040)
            ],
            new SettingsWindowPhysicalSize(520, 430),
            new SettingsPhysicalPoint(200, 200),
            lastRectangle);

        Assert.Equal(lastRectangle, placement.Rectangle);
        Assert.True(placement.ReusedLastPosition);
    }

    [Fact]
    public void SettingsWindowIgnoresInvalidOffscreenReusedPosition()
    {
        var placement = SettingsWindowPositionCalculator.Calculate(
            [new SettingsMonitorWorkArea(0, 0, 1920, 1040, IsPrimary: true)],
            new SettingsWindowPhysicalSize(520, 430),
            new SettingsPhysicalPoint(200, 200),
            new SettingsWindowPhysicalRect(-2000, 100, 520, 430));

        Assert.Equal(new SettingsWindowPhysicalRect(700, 305, 520, 430), placement.Rectangle);
        Assert.False(placement.ReusedLastPosition);
    }

    [Fact]
    public void SettingsWindowCenteringUsesSelectedMonitorNotVirtualDesktopBounds()
    {
        var placement = SettingsWindowPositionCalculator.Calculate(
            [
                new SettingsMonitorWorkArea(-1920, 0, 1920, 1040),
                new SettingsMonitorWorkArea(0, 0, 1920, 1040, IsPrimary: true)
            ],
            new SettingsWindowPhysicalSize(520, 430),
            new SettingsPhysicalPoint(-1000, 300),
            lastWindowRectangle: null);

        Assert.Equal(new SettingsWindowPhysicalRect(-1220, 305, 520, 430), placement.Rectangle);
        Assert.NotEqual(new SettingsWindowPhysicalRect(-260, 305, 520, 430), placement.Rectangle);
    }
}
