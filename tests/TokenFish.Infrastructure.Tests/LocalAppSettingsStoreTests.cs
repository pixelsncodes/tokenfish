using System.Text;
using System.Text.Json;
using TokenFish.Core.Models;
using TokenFish.Infrastructure;

namespace TokenFish.Infrastructure.Tests;

public sealed class LocalAppSettingsStoreTests
{
    [Fact]
    public async Task MissingFileReturnsExactDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public async Task ValidAllowlistedSettingsRoundTrip()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        var expected = new AppSettings
        {
            ProviderSelectionMode = ProviderSelectionMode.CodexOnly,
            ThemeMode = ThemeMode.Arcade,
            CodexRuntimeMode = CodexRuntimeMode.Wsl,
            CodexWslDistributionName = "Ubuntu-24.04"
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, loaded);
    }

    [Fact]
    public async Task PersistedJsonContainsOnlyApprovedProperties()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);

        await store.SaveAsync(new AppSettings(), CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(store.SettingsFilePath));
        var propertyNames = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            [
                "CodexRuntimeMode",
                "CodexWslDistributionName",
                "ProviderSelectionMode",
                "SchemaVersion",
                "ThemeMode"
            ],
            propertyNames);
    }

    [Fact]
    public async Task SensitiveOrUnrelatedPropertiesCannotAppearThroughAutomaticModelSerialization()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);

        await store.SaveAsync(new AppSettings(), CancellationToken.None);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(store.SettingsFilePath));
        var propertyNames = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        var forbiddenFragments = new[]
        {
            "api",
            "key",
            "credential",
            "token",
            "prompt",
            "response",
            "workspace",
            "path",
            "command",
            "environment",
            "snapshot",
            "exception",
            "log"
        };

        foreach (var forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(
                propertyNames,
                propertyName => propertyName.Contains(
                    forbiddenFragment,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task WhitespaceDistributionNormalizesToNull()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);

        await store.SaveAsync(
            new AppSettings { CodexWslDistributionName = " " },
            CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Null(loaded.CodexWslDistributionName);
    }

    [Fact]
    public async Task InvalidNativeModeDistributionCombinationRecoversSafelyOnLoad()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        await WriteSettingsJsonAsync(
            store.SettingsFilePath,
            """
            {
              "SchemaVersion": 1,
              "ProviderSelectionMode": "CodexOnly",
              "ThemeMode": "Arcade",
              "CodexRuntimeMode": "NativeWindows",
              "CodexWslDistributionName": "Ubuntu"
            }
            """);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public async Task InvalidSettingsAreRejectedBeforeSave()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        await store.SaveAsync(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            CancellationToken.None);
        var before = await File.ReadAllTextAsync(store.SettingsFilePath);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                new AppSettings
                {
                    CodexRuntimeMode = CodexRuntimeMode.NativeWindows,
                    CodexWslDistributionName = "Ubuntu"
                },
                CancellationToken.None));

        Assert.Equal(before, await File.ReadAllTextAsync(store.SettingsFilePath));
    }

    [Fact]
    public async Task UnknownEnumRecoversToDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        await WriteSettingsJsonAsync(
            store.SettingsFilePath,
            """
            {
              "SchemaVersion": 1,
              "ProviderSelectionMode": "Both",
              "ThemeMode": "FutureTheme",
              "CodexRuntimeMode": "WslLoginShell",
              "CodexWslDistributionName": null
            }
            """);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public async Task MalformedJsonRecoversToDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        await WriteSettingsJsonAsync(store.SettingsFilePath, "{");

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public async Task EmptyJsonFileRecoversToDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(store.SettingsFilePath, string.Empty);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public async Task UnsupportedSchemaVersionRecoversToDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        await WriteSettingsJsonAsync(
            store.SettingsFilePath,
            """
            {
              "SchemaVersion": 999,
              "ProviderSelectionMode": "CodexOnly",
              "ThemeMode": "Arcade",
              "CodexRuntimeMode": "Wsl",
              "CodexWslDistributionName": "Ubuntu"
            }
            """);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public async Task UnknownExtraJsonFieldsDoNotAlterAllowedSettings()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        await WriteSettingsJsonAsync(
            store.SettingsFilePath,
            """
            {
              "SchemaVersion": 1,
              "ProviderSelectionMode": "CodexOnly",
              "ThemeMode": "Arcade",
              "CodexRuntimeMode": "Wsl",
              "CodexWslDistributionName": "Ubuntu",
              "WorkspacePath": "C:\\Users\\pixel\\secret",
              "AccessToken": "not-used"
            }
            """);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(ProviderSelectionMode.CodexOnly, settings.ProviderSelectionMode);
        Assert.Equal(ThemeMode.Arcade, settings.ThemeMode);
        Assert.Equal(CodexRuntimeMode.Wsl, settings.CodexRuntimeMode);
        Assert.Equal("Ubuntu", settings.CodexWslDistributionName);
    }

    [Fact]
    public async Task FailedSerializationDoesNotReplaceExistingValidFile()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        await store.SaveAsync(
            new AppSettings { ProviderSelectionMode = ProviderSelectionMode.CodexOnly },
            CancellationToken.None);
        var before = await File.ReadAllTextAsync(store.SettingsFilePath);
        var failingStore = new LocalAppSettingsStore(
            store.SettingsFilePath,
            async (stream, _, cancellationToken) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("{"), cancellationToken);
                throw new InvalidOperationException("synthetic serialization failure");
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failingStore.SaveAsync(
                new AppSettings { ProviderSelectionMode = ProviderSelectionMode.ClaudeOnly },
                CancellationToken.None));

        Assert.Equal(before, await File.ReadAllTextAsync(store.SettingsFilePath));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task CancellationIsRespected()
    {
        using var directory = TemporaryDirectory.Create();
        var store = CreateStore(directory);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.LoadAsync(cancellationTokenSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(new AppSettings(), cancellationTokenSource.Token));
    }

    [Fact]
    public void ConstructionDoesNotCreateFilesOrDirectories()
    {
        using var directory = TemporaryDirectory.Create(deleteImmediately: true);

        _ = CreateStore(directory);

        Assert.False(Directory.Exists(directory.Path));
    }

    private static LocalAppSettingsStore CreateStore(TemporaryDirectory directory) =>
        new(Path.Combine(directory.Path, "settings.json"));

    private static async Task WriteSettingsJsonAsync(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path, bool deleteImmediately)
        {
            Path = path;

            if (!deleteImmediately)
            {
                Directory.CreateDirectory(path);
            }
        }

        public string Path { get; }

        public static TemporaryDirectory Create(bool deleteImmediately = false) =>
            new(
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"TokenFish-{Guid.NewGuid():N}"),
                deleteImmediately);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
