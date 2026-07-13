using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class SettingsWindowLayoutCalculatorTests
{
    [Theory]
    [InlineData(1.0, 520)]
    [InlineData(1.25, 650)]
    [InlineData(1.5, 780)]
    [InlineData(2.0, 1040)]
    public void EffectiveWidthConvertsToPhysicalPixels(double scale, int expected)
    {
        var physical = SettingsWindowLayoutCalculator.EffectiveToPhysicalPixels(
            SettingsWindowLayoutCalculator.WidthEffectivePixels,
            scale);

        Assert.Equal(expected, physical);
    }

    [Theory]
    [InlineData(1.0, 390, 8, 40, 528, 430)]
    [InlineData(1.25, 390, 10, 50, 660, 538)]
    [InlineData(1.5, 390, 12, 60, 792, 645)]
    [InlineData(2.0, 390, 16, 80, 1056, 860)]
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
        Assert.Equal(390, layout.ClientHeightEffectivePixels);
    }

    [Fact]
    public void FractionalMeasuredHeightRoundsUpBeforeScaling()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 390.1,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 0,
            nonClientHeightPhysicalPixels: 0);

        Assert.Equal(489, layout.Size.Height);
        Assert.Equal(391, layout.ClientHeightEffectivePixels);
    }

    [Fact]
    public void VeryTallAccessibilityContentIsBoundedByMaximumClientHeight()
    {
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels: 800,
            rasterizationScale: 1.25,
            nonClientWidthPhysicalPixels: 10,
            nonClientHeightPhysicalPixels: 50);

        Assert.Equal(750, layout.Size.Height);
        Assert.Equal(SettingsWindowLayoutCalculator.MaximumClientHeightEffectivePixels, layout.ClientHeightEffectivePixels);
    }
}
