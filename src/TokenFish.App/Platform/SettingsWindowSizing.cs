using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using TokenFish.Infrastructure;
using Windows.Graphics;
using WinRT.Interop;

namespace TokenFish.App.Platform;

internal static class SettingsWindowSizing
{
    public static double GetRasterizationScale(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = WindowNative.GetWindowHandle(window);
        return window.Content.XamlRoot?.RasterizationScale ??
            NativeMethods.GetDpiForWindow(handle) / 96.0;
    }

    public static SizeInt32 ToPhysicalSize(
        double widthEffectivePixels,
        double heightEffectivePixels,
        double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        return new SizeInt32(
            EffectiveToPhysicalPixels(widthEffectivePixels, scale),
            EffectiveToPhysicalPixels(heightEffectivePixels, scale));
    }

    public static SizeInt32 ToOuterPhysicalSize(
        Window window,
        double measuredClientHeightEffectivePixels,
        RectInt32 targetWorkArea)
    {
        ArgumentNullException.ThrowIfNull(window);

        var scale = GetRasterizationScale(window);
        var nonClientSize = GetNonClientSize(window);
        var safeMarginPhysicalPixels = EffectiveToPhysicalPixels(
            SettingsWindowLayoutCalculator.WorkAreaMarginEffectivePixels,
            scale);
        var maximumWindowHeightPhysicalPixels = Math.Max(
            1,
            targetWorkArea.Height - (safeMarginPhysicalPixels * 2));
        var layout = SettingsWindowLayoutCalculator.Calculate(
            measuredClientHeightEffectivePixels,
            scale,
            nonClientSize.Width,
            nonClientSize.Height,
            maximumWindowHeightPhysicalPixels);

        return new SizeInt32(layout.Size.Width, layout.Size.Height);
    }

    private static int EffectiveToPhysicalPixels(double effectivePixels, double scale)
    {
        if (effectivePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(effectivePixels));
        }

        return Math.Max(1, (int)Math.Ceiling(effectivePixels * scale));
    }

    private static SizeInt32 GetNonClientSize(Window window)
    {
        var handle = WindowNative.GetWindowHandle(window);
        if (handle == 0 ||
            !NativeMethods.GetWindowRect(handle, out var windowRect) ||
            !NativeMethods.GetClientRect(handle, out var clientRect))
        {
            return new SizeInt32(0, 0);
        }

        return new SizeInt32(
            Math.Max(0, windowRect.Width - clientRect.Width),
            Math.Max(0, windowRect.Height - clientRect.Height));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(nint hWnd, out NativeRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(nint hWnd, out NativeRect rect);
    }
}
