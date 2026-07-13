namespace TokenFish.Infrastructure;

public static class SettingsWindowLayoutCalculator
{
    public const double WidthEffectivePixels = 680;
    public const double WorkAreaMarginEffectivePixels = 24;
    public const double HeightRoundingAllowanceEffectivePixels = 2;

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
        int nonClientHeightPhysicalPixels,
        int? maximumWindowHeightPhysicalPixels = null)
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

        if (maximumWindowHeightPhysicalPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWindowHeightPhysicalPixels));
        }

        var measuredHeight = Math.Ceiling(measuredClientHeightEffectivePixels);
        // WinUI can report fractional desired sizes while the final AppWindow size is integral
        // physical pixels. A fixed 2-DIP allowance prevents a 1px rounding mismatch from
        // lighting up the ScrollViewer without accumulating across repeated activations.
        var desiredClientHeightEffectivePixels = Math.Ceiling(
            measuredClientHeightEffectivePixels + HeightRoundingAllowanceEffectivePixels);
        var maximumClientHeightEffectivePixels = desiredClientHeightEffectivePixels;
        if (maximumWindowHeightPhysicalPixels is { } maximumWindowHeight)
        {
            var maximumClientHeightPhysicalPixels = Math.Max(
                1,
                maximumWindowHeight - nonClientHeightPhysicalPixels);
            maximumClientHeightEffectivePixels = Math.Min(
                maximumClientHeightEffectivePixels,
                Math.Floor(maximumClientHeightPhysicalPixels / rasterizationScale));
        }

        var clientHeightEffectivePixels = Math.Min(
            desiredClientHeightEffectivePixels,
            Math.Max(1, maximumClientHeightEffectivePixels));
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
            clientHeightEffectivePixels,
            clientHeightEffectivePixels < measuredHeight);
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
    double ClientHeightEffectivePixels,
    bool RequiresVerticalScroll);

public static class SettingsWindowPositionCalculator
{
    public static SettingsWindowPlacement Calculate(
        IReadOnlyList<SettingsMonitorWorkArea> workAreas,
        SettingsWindowPhysicalSize windowSize,
        SettingsPhysicalPoint? preferredAnchor,
        SettingsWindowPhysicalRect? lastWindowRectangle)
    {
        ArgumentNullException.ThrowIfNull(workAreas);

        if (workAreas.Count == 0)
        {
            throw new ArgumentException("At least one work area is required.", nameof(workAreas));
        }

        ValidateWindowSize(windowSize);

        if (lastWindowRectangle is { } lastRectangle &&
            TryFindContainingWorkArea(workAreas, lastRectangle, out var reuseWorkArea))
        {
            return new SettingsWindowPlacement(
                ClampToWorkArea(lastRectangle, reuseWorkArea),
                ReusedLastPosition: true);
        }

        var workArea = SelectWorkArea(workAreas, preferredAnchor);
        var centered = CenterInWorkArea(windowSize, workArea);
        return new SettingsWindowPlacement(
            ClampToWorkArea(centered, workArea),
            ReusedLastPosition: false);
    }

    public static SettingsWindowPhysicalRect CenterInWorkArea(
        SettingsWindowPhysicalSize windowSize,
        SettingsMonitorWorkArea workArea)
    {
        ValidateWindowSize(windowSize);
        ValidateWorkArea(workArea);

        return new SettingsWindowPhysicalRect(
            workArea.X + Math.Max(0, (workArea.Width - windowSize.Width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - windowSize.Height) / 2),
            windowSize.Width,
            windowSize.Height);
    }

    public static SettingsWindowPhysicalRect ClampToWorkArea(
        SettingsWindowPhysicalRect rectangle,
        SettingsMonitorWorkArea workArea)
    {
        ValidateRectangle(rectangle);
        ValidateWorkArea(workArea);

        var maxX = workArea.X + Math.Max(0, workArea.Width - rectangle.Width);
        var maxY = workArea.Y + Math.Max(0, workArea.Height - rectangle.Height);
        return rectangle with
        {
            X = Math.Clamp(rectangle.X, workArea.X, maxX),
            Y = Math.Clamp(rectangle.Y, workArea.Y, maxY)
        };
    }

    private static SettingsMonitorWorkArea SelectWorkArea(
        IReadOnlyList<SettingsMonitorWorkArea> workAreas,
        SettingsPhysicalPoint? preferredAnchor)
    {
        if (preferredAnchor is { } anchor)
        {
            foreach (var workArea in workAreas)
            {
                if (workArea.Contains(anchor))
                {
                    return workArea;
                }
            }
        }

        foreach (var workArea in workAreas)
        {
            if (workArea.IsPrimary)
            {
                return workArea;
            }
        }

        return workAreas[0];
    }

    private static bool TryFindContainingWorkArea(
        IReadOnlyList<SettingsMonitorWorkArea> workAreas,
        SettingsWindowPhysicalRect rectangle,
        out SettingsMonitorWorkArea containingWorkArea)
    {
        foreach (var workArea in workAreas)
        {
            if (workArea.Contains(rectangle))
            {
                containingWorkArea = workArea;
                return true;
            }
        }

        containingWorkArea = default;
        return false;
    }

    private static void ValidateWindowSize(SettingsWindowPhysicalSize windowSize)
    {
        if (windowSize.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize));
        }

        if (windowSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize));
        }
    }

    private static void ValidateRectangle(SettingsWindowPhysicalRect rectangle)
    {
        if (rectangle.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        }

        if (rectangle.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        }
    }

    private static void ValidateWorkArea(SettingsMonitorWorkArea workArea)
    {
        if (workArea.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea));
        }

        if (workArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea));
        }
    }
}

public readonly record struct SettingsPhysicalPoint(int X, int Y);

public readonly record struct SettingsWindowPhysicalRect(
    int X,
    int Y,
    int Width,
    int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public SettingsPhysicalPoint Center => new(X + Width / 2, Y + Height / 2);
}

public readonly record struct SettingsMonitorWorkArea(
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary = false)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(SettingsPhysicalPoint point) =>
        point.X >= X &&
        point.X < Right &&
        point.Y >= Y &&
        point.Y < Bottom;

    public bool Contains(SettingsWindowPhysicalRect rectangle) =>
        rectangle.X >= X &&
        rectangle.Y >= Y &&
        rectangle.Right <= Right &&
        rectangle.Bottom <= Bottom;
}

public readonly record struct SettingsWindowPlacement(
    SettingsWindowPhysicalRect Rectangle,
    bool ReusedLastPosition);
