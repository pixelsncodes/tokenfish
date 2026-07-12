using TokenFish.Core.Models;

namespace TokenFish.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void DefaultsSelectBothProvidersAndMinimalTheme()
    {
        var settings = new AppSettings();

        Assert.Equal(ProviderSelectionMode.Both, settings.ProviderSelectionMode);
        Assert.Equal(ThemeMode.Minimal, settings.ThemeMode);
    }

    [Fact]
    public void ExposesNoCredentialOrSecretProperties()
    {
        var forbiddenFragments = new[]
        {
            "api",
            "key",
            "secret",
            "credential",
            "password",
            "cookie",
            "token",
            "session",
            "prompt",
            "response",
            "source",
            "history",
            "email",
            "username",
            "workspace",
            "path"
        };

        var propertyNames = typeof(AppSettings)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (var propertyName in propertyNames)
        {
            Assert.DoesNotContain(
                forbiddenFragments,
                fragment => propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ProviderSelectionModesRemainValid()
    {
        Assert.Equal(
            new[] { "ClaudeOnly", "CodexOnly", "Both" },
            Enum.GetNames<ProviderSelectionMode>());
    }

    [Fact]
    public void SecurityRelatedEnumsContainExpectedValues()
    {
        Assert.Equal(
            new[] { "UserControlled", "LocalProviderReported", "TokenFishDerived" },
            Enum.GetNames<DataAuthority>());

        Assert.Equal(
            new[] { "Unknown", "Live", "Cached", "Stale" },
            Enum.GetNames<DataFreshness>());

        Assert.Equal(
            new[] { "Unknown", "NotConfigured", "Disconnected", "Connecting", "Connected", "Degraded" },
            Enum.GetNames<ProviderConnectionState>());
    }
}
