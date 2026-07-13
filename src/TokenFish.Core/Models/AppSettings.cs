namespace TokenFish.Core.Models;

public sealed record AppSettings
{
    public bool IsOnboardingCompleted { get; init; }

    public ProviderSelectionMode ProviderSelectionMode { get; init; } = ProviderSelectionMode.CodexOnly;

    public ThemeMode ThemeMode { get; init; } = ThemeMode.Minimal;

    public CodexRuntimeMode CodexRuntimeMode { get; init; } = CodexRuntimeMode.WslLoginShell;

    public string? CodexWslDistributionName { get; init; }
}
