namespace TokenFish.Infrastructure;

public static class PopupWindowLayoutCalculator
{
    public const double WidthEffectivePixels = 380;
    public const int WorkAreaMarginPhysicalPixels = 8;

    public static int EffectiveToPhysicalPixels(double effectivePixels, double rasterizationScale)
    {
        ValidateScale(rasterizationScale);

        return Math.Max(1, (int)Math.Ceiling(effectivePixels * rasterizationScale));
    }

    public static double PhysicalToEffectivePixels(int physicalPixels, double rasterizationScale)
    {
        ValidateScale(rasterizationScale);

        return physicalPixels / rasterizationScale;
    }

    public static PopupWindowLayout Calculate(
        PopupPhysicalRect anchor,
        PopupPhysicalRect workArea,
        double rasterizationScale,
        double measuredContentHeightEffectivePixels,
        int nonClientHeightPhysicalPixels = 0)
    {
        ValidateScale(rasterizationScale);

        if (measuredContentHeightEffectivePixels < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(measuredContentHeightEffectivePixels),
                "Measured client height cannot be negative.");
        }

        if (nonClientHeightPhysicalPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nonClientHeightPhysicalPixels));
        }

        var width = EffectiveToPhysicalPixels(WidthEffectivePixels, rasterizationScale);
        var maximumOuterHeight = Math.Max(
            1,
            workArea.Height - (WorkAreaMarginPhysicalPixels * 2));
        var maximumClientHeight = Math.Max(
            1,
            maximumOuterHeight - nonClientHeightPhysicalPixels);
        var desiredClientHeight = EffectiveToPhysicalPixels(
            measuredContentHeightEffectivePixels,
            rasterizationScale);
        var clientHeight = Math.Min(desiredClientHeight, maximumClientHeight);
        var height = Math.Min(
            maximumOuterHeight,
            clientHeight + nonClientHeightPhysicalPixels);
        var heightEffective = PhysicalToEffectivePixels(clientHeight, rasterizationScale);

        var left = anchor.X + (anchor.Width / 2) - (width / 2);
        var top = anchor.Y - height - WorkAreaMarginPhysicalPixels;

        if (anchor.Y <= workArea.Y + WorkAreaMarginPhysicalPixels)
        {
            top = anchor.Y + anchor.Height + WorkAreaMarginPhysicalPixels;
        }
        else if (anchor.X <= workArea.X + WorkAreaMarginPhysicalPixels)
        {
            left = anchor.X + anchor.Width + WorkAreaMarginPhysicalPixels;
            top = anchor.Y + (anchor.Height / 2) - (height / 2);
        }
        else if (anchor.X + anchor.Width >=
            workArea.X + workArea.Width - WorkAreaMarginPhysicalPixels)
        {
            left = anchor.X - width - WorkAreaMarginPhysicalPixels;
            top = anchor.Y + (anchor.Height / 2) - (height / 2);
        }

        left = Clamp(left, workArea.X, workArea.X + workArea.Width - width);
        top = Clamp(top, workArea.Y, workArea.Y + workArea.Height - height);

        return new PopupWindowLayout(
            new PopupPhysicalPoint(left, top),
            new PopupPhysicalSize(width, height),
            heightEffective);
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

    private static int Clamp(int value, int minimum, int maximum) =>
        Math.Min(Math.Max(value, minimum), maximum);

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Min(Math.Max(value, minimum), maximum);
}

public readonly record struct PopupPhysicalPoint(int X, int Y);

public readonly record struct PopupPhysicalSize(int Width, int Height);

public readonly record struct PopupPhysicalRect(int X, int Y, int Width, int Height);

public readonly record struct PopupWindowLayout(
    PopupPhysicalPoint Position,
    PopupPhysicalSize Size,
    double HeightEffectivePixels);
