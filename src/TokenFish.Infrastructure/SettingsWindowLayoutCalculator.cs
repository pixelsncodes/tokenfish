namespace TokenFish.Infrastructure;

public static class SettingsWindowLayoutCalculator
{
    public const double WidthEffectivePixels = 520;
    public const double MaximumClientHeightEffectivePixels = 560;

    public static int EffectiveToPhysicalPixels(double effectivePixels, double rasterizationScale)
    {
        ValidateScale(rasterizationScale);

        if (effectivePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(effectivePixels));
        }

        return Math.Max(1, (int)Math.Ceiling(effectivePixels * rasterizationScale));
    }

    public static SettingsWindowLayout Calculate(
        double measuredClientHeightEffectivePixels,
        double rasterizationScale,
        int nonClientWidthPhysicalPixels,
        int nonClientHeightPhysicalPixels)
    {
        ValidateScale(rasterizationScale);

        if (measuredClientHeightEffectivePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(measuredClientHeightEffectivePixels));
        }

        if (nonClientWidthPhysicalPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nonClientWidthPhysicalPixels));
        }

        if (nonClientHeightPhysicalPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nonClientHeightPhysicalPixels));
        }

        var clientHeightEffectivePixels = Math.Min(
            Math.Ceiling(measuredClientHeightEffectivePixels),
            MaximumClientHeightEffectivePixels);
        var clientWidthPhysicalPixels = EffectiveToPhysicalPixels(
            WidthEffectivePixels,
            rasterizationScale);
        var clientHeightPhysicalPixels = EffectiveToPhysicalPixels(
            clientHeightEffectivePixels,
            rasterizationScale);

        return new SettingsWindowLayout(
            new SettingsWindowPhysicalSize(
                clientWidthPhysicalPixels + nonClientWidthPhysicalPixels,
                clientHeightPhysicalPixels + nonClientHeightPhysicalPixels),
            clientHeightEffectivePixels);
    }

    private static void ValidateScale(double rasterizationScale)
    {
        if (double.IsNaN(rasterizationScale) ||
            double.IsInfinity(rasterizationScale) ||
            rasterizationScale <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rasterizationScale),
                "Rasterization scale must be greater than zero.");
        }
    }
}

public readonly record struct SettingsWindowPhysicalSize(int Width, int Height);

public readonly record struct SettingsWindowLayout(
    SettingsWindowPhysicalSize Size,
    double ClientHeightEffectivePixels);
