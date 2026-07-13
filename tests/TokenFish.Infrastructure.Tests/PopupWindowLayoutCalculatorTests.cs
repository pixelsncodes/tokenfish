using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class PopupWindowLayoutCalculatorTests
{
    [Theory]
    [InlineData(1.0, 380)]
    [InlineData(1.25, 475)]
    [InlineData(1.5, 570)]
    public void EffectiveWidthConvertsToPhysicalPixels(double scale, int expected)
    {
        var physical = PopupWindowLayoutCalculator.EffectiveToPhysicalPixels(
            PopupWindowLayoutCalculator.WidthEffectivePixels,
            scale);

        Assert.Equal(expected, physical);
    }

    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(125, 1.25, 100)]
    [InlineData(150, 1.5, 100)]
    public void PhysicalPixelsConvertToEffectivePixels(
        int physicalPixels,
        double scale,
        double expected)
    {
        var effective = PopupWindowLayoutCalculator.PhysicalToEffectivePixels(
            physicalPixels,
            scale);

        Assert.Equal(expected, effective);
    }

    [Fact]
    public void ContentHeightDeterminesCompactWindowHeightWithoutUsingWorkAreaHeight()
    {
        var layout = Calculate(measuredContentHeightEffectivePixels: 320, scale: 1.25);

        Assert.Equal(400, layout.Size.Height);
        Assert.Equal(320, layout.HeightEffectivePixels);
    }

    [Theory]
    [InlineData(1.0, 180)]
    [InlineData(1.25, 225)]
    [InlineData(1.5, 270)]
    public void ShortWaitingStateUsesMinimumHeight(double scale, int expectedPhysicalHeight)
    {
        var layout = Calculate(measuredContentHeightEffectivePixels: 120, scale);

        Assert.Equal(expectedPhysicalHeight, layout.Size.Height);
        Assert.Equal(PopupWindowLayoutCalculator.MinimumHeightEffectivePixels, layout.HeightEffectivePixels);
    }

    [Fact]
    public void NormalOneWindowStateFitsMeasuredContent()
    {
        var layout = Calculate(measuredContentHeightEffectivePixels: 300, scale: 1.0);

        Assert.Equal(300, layout.Size.Height);
    }

    [Fact]
    public void ActivityPresentCanIncreaseHeightWithoutUsingMaximum()
    {
        var withoutActivity = Calculate(measuredContentHeightEffectivePixels: 250, scale: 1.0);
        var withActivity = Calculate(measuredContentHeightEffectivePixels: 330, scale: 1.0);

        Assert.True(withActivity.Size.Height > withoutActivity.Size.Height);
        Assert.True(withActivity.HeightEffectivePixels < PopupWindowLayoutCalculator.MaximumHeightEffectivePixels);
    }

    [Fact]
    public void MultipleQuotaWindowsRemainBoundedByMaximumHeight()
    {
        var layout = Calculate(measuredContentHeightEffectivePixels: 900, scale: 1.0);

        Assert.Equal(640, layout.Size.Height);
        Assert.Equal(PopupWindowLayoutCalculator.MaximumHeightEffectivePixels, layout.HeightEffectivePixels);
    }

    [Fact]
    public void MaximumHeightIsBoundedByWorkArea()
    {
        var layout = PopupWindowLayoutCalculator.Calculate(
            new PopupPhysicalRect(900, 700, 24, 24),
            new PopupPhysicalRect(0, 0, 1000, 420),
            rasterizationScale: 1.0,
            measuredContentHeightEffectivePixels: 900);

        Assert.Equal(404, layout.Size.Height);
        Assert.Equal(404, layout.HeightEffectivePixels);
    }

    [Fact]
    public void RasterizationScaleIsNotAppliedTwice()
    {
        var layout = Calculate(measuredContentHeightEffectivePixels: 320, scale: 1.25);

        Assert.Equal(400, layout.Size.Height);
        Assert.NotEqual(500, layout.Size.Height);
    }

    [Fact]
    public void PositionRemainsInsideDisplayWorkArea()
    {
        var workArea = new PopupPhysicalRect(0, 0, 1200, 800);
        var layout = PopupWindowLayoutCalculator.Calculate(
            new PopupPhysicalRect(1180, 780, 24, 24),
            workArea,
            rasterizationScale: 1.25,
            measuredContentHeightEffectivePixels: 340);

        Assert.InRange(layout.Position.X, workArea.X, workArea.X + workArea.Width - layout.Size.Width);
        Assert.InRange(layout.Position.Y, workArea.Y, workArea.Y + workArea.Height - layout.Size.Height);
    }

    private static PopupWindowLayout Calculate(
        double measuredContentHeightEffectivePixels,
        double scale) =>
        PopupWindowLayoutCalculator.Calculate(
            new PopupPhysicalRect(900, 700, 24, 24),
            new PopupPhysicalRect(0, 0, 1200, 800),
            scale,
            measuredContentHeightEffectivePixels);
}
