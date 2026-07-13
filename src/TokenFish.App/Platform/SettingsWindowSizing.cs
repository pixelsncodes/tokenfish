using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
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

    private static int EffectiveToPhysicalPixels(double effectivePixels, double scale)
    {
        if (effectivePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(effectivePixels));
        }

        return Math.Max(1, (int)Math.Ceiling(effectivePixels * scale));
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(nint hWnd);
    }
}
