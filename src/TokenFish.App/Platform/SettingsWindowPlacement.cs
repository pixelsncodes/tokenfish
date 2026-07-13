using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TokenFish.Infrastructure;
using Windows.Graphics;

namespace TokenFish.App.Platform;

internal static class SettingsWindowPlacementService
{
    private static readonly object Sync = new();
    private static SettingsWindowPhysicalRect? lastWindowRectangle;

    public static void EnsureVisibleOnMonitor(Window window, RectInt32? preferredAnchor)
    {
        ArgumentNullException.ThrowIfNull(window);

        var windowRectangle = GetWindowRectangle(window);
        if (windowRectangle.Width <= 0 || windowRectangle.Height <= 0)
        {
            return;
        }

        var reused = TryReuseLastPosition(window, windowRectangle);
        if (reused)
        {
            return;
        }

        var displayArea = GetPreferredDisplayArea(preferredAnchor);
        var workArea = ToWorkArea(displayArea.WorkArea, isPrimary: true);
        SettingsPhysicalPoint? anchor = preferredAnchor is { } anchorRectangle
            ? new SettingsPhysicalPoint(
                anchorRectangle.X + anchorRectangle.Width / 2,
                anchorRectangle.Y + anchorRectangle.Height / 2)
            : null;
        var placement = SettingsWindowPositionCalculator.Calculate(
            [workArea],
            new SettingsWindowPhysicalSize(windowRectangle.Width, windowRectangle.Height),
            anchor,
            lastWindowRectangle: null);

        Move(window, placement.Rectangle);
    }

    public static bool IsCurrentPositionVisible(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var rectangle = GetWindowRectangle(window);
        var displayArea = DisplayArea.GetFromRect(ToRectInt32(rectangle), DisplayAreaFallback.Nearest);
        var workArea = ToWorkArea(displayArea.WorkArea, isPrimary: true);
        return workArea.Contains(rectangle);
    }

    public static void CaptureCurrentPosition(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var rectangle = GetWindowRectangle(window);
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        var displayArea = DisplayArea.GetFromRect(ToRectInt32(rectangle), DisplayAreaFallback.Nearest);
        var workArea = ToWorkArea(displayArea.WorkArea, isPrimary: true);
        if (!workArea.Contains(rectangle))
        {
            return;
        }

        lock (Sync)
        {
            lastWindowRectangle = rectangle;
        }
    }

    private static bool TryReuseLastPosition(
        Window window,
        SettingsWindowPhysicalRect currentRectangle)
    {
        SettingsWindowPhysicalRect? lastRectangle;
        lock (Sync)
        {
            lastRectangle = lastWindowRectangle;
        }

        if (lastRectangle is null)
        {
            return false;
        }

        var displayArea = DisplayArea.GetFromRect(
            ToRectInt32(lastRectangle.Value),
            DisplayAreaFallback.Nearest);
        var workArea = ToWorkArea(displayArea.WorkArea, isPrimary: true);
        var placement = SettingsWindowPositionCalculator.Calculate(
            [workArea],
            new SettingsWindowPhysicalSize(currentRectangle.Width, currentRectangle.Height),
            preferredAnchor: null,
            lastRectangle);

        if (!placement.ReusedLastPosition)
        {
            return false;
        }

        Move(window, placement.Rectangle);
        return true;
    }

    private static DisplayArea GetPreferredDisplayArea(RectInt32? preferredAnchor)
    {
        if (preferredAnchor is { } anchor)
        {
            return DisplayArea.GetFromRect(anchor, DisplayAreaFallback.Nearest);
        }

        if (NativeMethods.GetCursorPos(out var point))
        {
            return DisplayArea.GetFromPoint(
                new PointInt32(point.X, point.Y),
                DisplayAreaFallback.Nearest);
        }

        return DisplayArea.GetFromPoint(new PointInt32(0, 0), DisplayAreaFallback.Primary);
    }

    private static SettingsWindowPhysicalRect GetWindowRectangle(Window window)
    {
        var position = window.AppWindow.Position;
        var size = window.AppWindow.Size;
        return new SettingsWindowPhysicalRect(position.X, position.Y, size.Width, size.Height);
    }

    private static SettingsMonitorWorkArea ToWorkArea(RectInt32 workArea, bool isPrimary) =>
        new(workArea.X, workArea.Y, workArea.Width, workArea.Height, isPrimary);

    private static RectInt32 ToRectInt32(SettingsWindowPhysicalRect rectangle) =>
        new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    private static void Move(Window window, SettingsWindowPhysicalRect rectangle) =>
        window.AppWindow.Move(new PointInt32(rectangle.X, rectangle.Y));

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out Point point);
    }
}
