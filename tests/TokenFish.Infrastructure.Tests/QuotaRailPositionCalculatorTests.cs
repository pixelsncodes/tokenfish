using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class QuotaRailPositionCalculatorTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 140)]
    [InlineData(100, 280)]
    [InlineData(101, 280)]
    public void CrawlerOffsetIsClampedInsideRail(decimal percentage, double expectedOffset)
    {
        Assert.Equal(
            expectedOffset,
            QuotaRailPositionCalculator.CalculateCrawlerOffset(percentage, 300, 20));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(100, 274)]
    [InlineData(101, 274)]
    public void FishOffsetKeepsTheWiderMarkerInsideTheRail(decimal percentage, double expectedOffset)
    {
        Assert.Equal(
            expectedOffset,
            QuotaRailPositionCalculator.CalculateCrawlerOffset(percentage, 300, 26));
    }
}
