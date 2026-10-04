using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TokenFish.Core.Models;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace TokenFish.App;

internal static class TokenFishAppearance
{
    public static void Apply(FrameworkElement root, ThemeMode mode) => root.RequestedTheme = mode switch
    {
        ThemeMode.Light => ElementTheme.Light,
        ThemeMode.Dark or ThemeMode.Arcade => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    public static SolidColorBrush Brush(FrameworkElement root, string role)
    {
        if (new AccessibilitySettings().HighContrast)
        {
            var settings = new UISettings();
            return new(settings.GetColorValue(role is "Surface" or "Raised" ? UIColorType.Background : UIColorType.Foreground));
        }
        var dark = root.ActualTheme == ElementTheme.Dark;
        var hex = role switch
        {
            "Surface" => dark ? 0x161A21 : 0xFFFFFF,
            "Raised" => dark ? 0x20252E : 0xF3F5F8,
            "Muted" => dark ? 0xA3ADBC : 0x5E6A78,
            "Track" => dark ? 0x343B47 : 0xD9DFE7,
            "Brand" => dark ? 0xDDF87B : 0x607400,
            "Claude" => dark ? 0xE8AD8B : 0xA95531,
            "Codex" => dark ? 0x74D9C0 : 0x007968,
            _ => dark ? 0xEDF1F7 : 0x1B2531
        };
        return new(Color.FromArgb(255, (byte)(hex >> 16), (byte)(hex >> 8), (byte)hex));
    }
}
