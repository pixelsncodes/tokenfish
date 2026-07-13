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
    [InlineData(1.0, 120)]
    [InlineData(1.25, 150)]
    [InlineData(1.5, 180)]
    public void ShortWaitingStateUsesMeasuredHeight(double scale, int expectedPhysicalHeight)
    {
        var layout = Calculate(measuredContentHeightEffectivePixels: 120, scale);

        Assert.Equal(expectedPhysicalHeight, layout.Size.Height);
        Assert.Equal(120, layout.HeightEffectivePixels);
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
        Assert.Equal(330, withActivity.HeightEffectivePixels);
    }

    [Fact]
    public void ContentFitsWhenTheWorkAreaCanContainIt()
    {
        var layout = PopupWindowLayoutCalculator.Calculate(
            new PopupPhysicalRect(900, 700, 24, 24),
            new PopupPhysicalRect(0, 0, 1200, 1000),
            rasterizationScale: 1.0,
            measuredContentHeightEffectivePixels: 900);

        Assert.Equal(900, layout.Size.Height);
        Assert.Equal(900, layout.HeightEffectivePixels);
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

    [Theory]
    [InlineData(1.0, 360, 24, 384)]
    [InlineData(1.25, 360, 30, 480)]
    public void NonClientChromeIsIncludedExactlyOnce(
        double scale,
        double clientHeight,
        int nonClientHeight,
        int expectedOuterHeight)
    {
        var layout = PopupWindowLayoutCalculator.Calculate(
            new PopupPhysicalRect(900, 700, 24, 24),
            new PopupPhysicalRect(0, 0, 1200, 1000),
            scale,
            clientHeight,
            nonClientHeight);

        Assert.Equal(expectedOuterHeight, layout.Size.Height);
        Assert.Equal(clientHeight, layout.HeightEffectivePixels);
    }

    [Fact]
    public void FooterHeightIsIncludedExactlyOnceInTheMeasuredClientHeight()
    {
        const double providerContentHeight = 420;
        const double footerHeight = 48;

        var layout = Calculate(
            measuredContentHeightEffectivePixels: providerContentHeight + footerHeight,
            scale: 1.25);

        Assert.Equal(585, layout.Size.Height);
        Assert.Equal(providerContentHeight + footerHeight, layout.HeightEffectivePixels);
    }

    [Fact]
    public void RecalculationUsesCurrentContentHeight()
    {
        var expanded = Calculate(measuredContentHeightEffectivePixels: 560, scale: 1.25);
        var compact = Calculate(measuredContentHeightEffectivePixels: 320, scale: 1.25);

        Assert.Equal(700, expanded.Size.Height);
        Assert.Equal(400, compact.Size.Height);
    }

    [Fact]
    public void ConstrainedWorkAreaKeepsClientHeightPositive()
    {
        var layout = PopupWindowLayoutCalculator.Calculate(
            new PopupPhysicalRect(900, 700, 24, 24),
            new PopupPhysicalRect(0, 0, 1200, 20),
            1.25,
            900,
            nonClientHeightPhysicalPixels: 12);

        Assert.True(layout.Size.Height > 0);
        Assert.True(layout.HeightEffectivePixels > 0);
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
