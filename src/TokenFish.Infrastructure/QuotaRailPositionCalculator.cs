namespace TokenFish.Infrastructure;

public static class QuotaRailPositionCalculator
{
    public static double CalculateCrawlerOffset(
        decimal percentage,
        double railWidth,
        double crawlerWidth)
    {
        if (railWidth < 0 || crawlerWidth < 0)
        {
            throw new ArgumentOutOfRangeException();
        }

        var availableWidth = Math.Max(0, railWidth - crawlerWidth);
        var clampedPercentage = Math.Clamp(percentage, 0m, 100m);
        return availableWidth * (double)(clampedPercentage / 100m);
    }
}
