namespace TokenFish.Core.Models;

public sealed record AppSettings
{
    public ProviderSelectionMode ProviderSelectionMode { get; init; } = ProviderSelectionMode.Both;

    public ThemeMode ThemeMode { get; init; } = ThemeMode.Minimal;
}
