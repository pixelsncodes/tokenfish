namespace TokenFish.Core.Models;

public sealed record AppSettings
{
    public bool IsOnboardingCompleted { get; init; }

    public ProviderSelectionMode ProviderSelectionMode { get; init; } = ProviderSelectionMode.CodexOnly;

    public ThemeMode ThemeMode { get; init; } = ThemeMode.Minimal;

    public CodexRuntimeMode CodexRuntimeMode { get; init; } = CodexRuntimeMode.WslLoginShell;

    public string? CodexWslDistributionName { get; init; }

    public bool IsDesktopWidgetVisible { get; init; }
    public bool IsDesktopWidgetAlwaysOnTop { get; init; }
    public DesktopWidgetCorner DesktopWidgetCorner { get; init; } = DesktopWidgetCorner.BottomRight;
    public int? DesktopWidgetMonitorX { get; init; }
    public int? DesktopWidgetMonitorY { get; init; }
}

public enum DesktopWidgetCorner { BottomRight, BottomLeft, TopRight, TopLeft }
