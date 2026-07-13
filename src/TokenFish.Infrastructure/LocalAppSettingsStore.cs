using System.Text.Json;
using System.Text.Json.Serialization;
using TokenFish.Core.Models;
using TokenFish.Core.Settings;

namespace TokenFish.Infrastructure;

public sealed class LocalAppSettingsStore : IAppSettingsStore
{
    private const int CurrentSchemaVersion = 1;
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsFilePath;
    private readonly Func<Stream, PersistedAppSettings, CancellationToken, Task> _serializeAsync;

    public LocalAppSettingsStore()
        : this(GetDefaultSettingsFilePath())
    {
    }

    public LocalAppSettingsStore(string settingsFilePath)
        : this(settingsFilePath, SerializeAsync)
    {
    }

    internal LocalAppSettingsStore(
        string settingsFilePath,
        Func<Stream, PersistedAppSettings, CancellationToken, Task> serializeAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        ArgumentNullException.ThrowIfNull(serializeAsync);

        _settingsFilePath = Path.GetFullPath(settingsFilePath);
        _serializeAsync = serializeAsync;
    }

    public string SettingsFilePath => _settingsFilePath;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_settingsFilePath))
        {
            return new AppSettings();
        }

        PersistedAppSettings? persistedSettings;

        try
        {
            await using var stream = new FileStream(
                _settingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            if (stream.Length == 0)
            {
                return new AppSettings();
            }

            persistedSettings = await JsonSerializer.DeserializeAsync<PersistedAppSettings>(
                stream,
                LocalAppSettingsJsonContext.Default.PersistedAppSettings,
                cancellationToken);
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }

        return TryMapFromPersistence(persistedSettings, out var settings)
            ? settings
            : new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var normalizedSettings = AppSettingsValidator.Normalize(settings);
        var persistedSettings = MapToPersistence(normalizedSettings);

        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFilePath = Path.Combine(
            directory ?? Environment.CurrentDirectory,
            $"{SettingsFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await _serializeAsync(stream, persistedSettings, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(_settingsFilePath))
            {
                File.Replace(tempFilePath, _settingsFilePath, null);
            }
            else
            {
                File.Move(tempFilePath, _settingsFilePath);
            }
        }
        finally
        {
            TryDelete(tempFilePath);
        }
    }

    private static string GetDefaultSettingsFilePath()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var settingsDirectory = Path.Combine(localApplicationData, "TokenFish");

        return Path.Combine(settingsDirectory, SettingsFileName);
    }

    private static Task SerializeAsync(
        Stream stream,
        PersistedAppSettings settings,
        CancellationToken cancellationToken) =>
        JsonSerializer.SerializeAsync(
            stream,
            settings,
            LocalAppSettingsJsonContext.Default.PersistedAppSettings,
            cancellationToken);

    private static PersistedAppSettings MapToPersistence(AppSettings settings) =>
        new()
        {
            SchemaVersion = CurrentSchemaVersion,
            ProviderSelectionMode = settings.ProviderSelectionMode.ToString(),
            ThemeMode = settings.ThemeMode.ToString(),
            CodexRuntimeMode = settings.CodexRuntimeMode.ToString(),
            CodexWslDistributionName = settings.CodexWslDistributionName
        };

    private static bool TryMapFromPersistence(
        PersistedAppSettings? persistedSettings,
        out AppSettings settings)
    {
        settings = new AppSettings();

        if (persistedSettings is null ||
            persistedSettings.SchemaVersion != CurrentSchemaVersion ||
            !TryParseDefinedEnum(persistedSettings.ThemeMode, out ThemeMode themeMode) ||
            !TryParseDefinedEnum(
                persistedSettings.CodexRuntimeMode,
                out CodexRuntimeMode codexRuntimeMode))
        {
            return false;
        }

        var defaultSettings = new AppSettings();
        var providerSelectionMode = TryParseDefinedEnum(
            persistedSettings.ProviderSelectionMode,
            out ProviderSelectionMode persistedProviderSelectionMode)
            ? persistedProviderSelectionMode
            : defaultSettings.ProviderSelectionMode;

        var candidateSettings = new AppSettings
        {
            ProviderSelectionMode = providerSelectionMode,
            ThemeMode = themeMode,
            CodexRuntimeMode = codexRuntimeMode,
            CodexWslDistributionName = persistedSettings.CodexWslDistributionName
        };

        return AppSettingsValidator.TryNormalize(candidateSettings, out settings);
    }

    private static bool TryParseDefinedEnum<TEnum>(string? value, out TEnum enumValue)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse(value, ignoreCase: false, out enumValue) ||
            !Enum.IsDefined(enumValue))
        {
            enumValue = default;
            return false;
        }

        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed record PersistedAppSettings
{
    public int SchemaVersion { get; init; }

    public string? ProviderSelectionMode { get; init; }

    public string? ThemeMode { get; init; }

    public string? CodexRuntimeMode { get; init; }

    public string? CodexWslDistributionName { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PersistedAppSettings))]
internal sealed partial class LocalAppSettingsJsonContext : JsonSerializerContext;
