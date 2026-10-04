using TokenFish.Core.Models;

namespace TokenFish.Core.Settings;

public static class AppSettingsValidator
{
    public static AppSettings Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        EnsureDefined(settings.ProviderSelectionMode, nameof(settings.ProviderSelectionMode));
        EnsureDefined(settings.ThemeMode, nameof(settings.ThemeMode));
        EnsureDefined(settings.CodexRuntimeMode, nameof(settings.CodexRuntimeMode));
        EnsureDefined(settings.DesktopWidgetCorner, nameof(settings.DesktopWidgetCorner));

        var distributionName = string.IsNullOrWhiteSpace(settings.CodexWslDistributionName)
            ? null
            : settings.CodexWslDistributionName;

        if (settings.CodexRuntimeMode == CodexRuntimeMode.NativeWindows &&
            distributionName is not null)
        {
            throw new ArgumentException(
                "Codex WSL distribution cannot be used with native runtime mode.",
                nameof(settings));
        }

        return settings with
        {
            CodexWslDistributionName = distributionName
        };
    }

    public static bool TryNormalize(AppSettings settings, out AppSettings normalized)
    {
        try
        {
            normalized = Normalize(settings);
            return true;
        }
        catch (ArgumentException)
        {
            normalized = new AppSettings();
            return false;
        }
    }

    private static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, null);
        }
    }
}
