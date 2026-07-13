using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenFish.Providers.Claude;

internal sealed class ClaudeBridgeStateFileStore : IClaudeBridgeStateStore
{
    private const int CurrentSchemaVersion = 1;
    private const long MaximumStateFileBytes = 64 * 1024;
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMilliseconds(500);
    private const string MutexName = @"Local\TokenFish.ClaudeBridge.StatusV1";

    private readonly string _stateFilePath;
    private readonly Func<Mutex> _createMutex;

    public ClaudeBridgeStateFileStore()
        : this(new ClaudeBridgeStateFilePathResolver().GetDefaultStateFilePath())
    {
    }

    public ClaudeBridgeStateFileStore(string stateFilePath)
        : this(stateFilePath, static () => new Mutex(false, MutexName))
    {
    }

    internal ClaudeBridgeStateFileStore(
        string stateFilePath,
        Func<Mutex> createMutex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFilePath);
        ArgumentNullException.ThrowIfNull(createMutex);

        _stateFilePath = Path.GetFullPath(stateFilePath);
        _createMutex = createMutex;
    }

    public string StateFilePath => _stateFilePath;

    public Task<ClaudeBridgeState> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LoadUnsafe());
    }

    public Task<ClaudeBridgeState> MergeAndSaveAsync(
        ClaudeBridgeState incomingState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incomingState);
        cancellationToken.ThrowIfCancellationRequested();

        if (!incomingState.HasUsageData)
        {
            return Task.FromResult(LoadUnsafe());
        }

        using var mutex = _createMutex();
        var hasLock = false;

        try
        {
            try
            {
                hasLock = mutex.WaitOne(LockTimeout);
            }
            catch (AbandonedMutexException)
            {
                hasLock = true;
            }

            if (!hasLock)
            {
                throw new ClaudeBridgeStateStoreException(ClaudeBridgeStateStoreFailureKind.Unreadable);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var existingState = LoadUnsafe();
            var mergedState = Merge(existingState, incomingState);
            SaveUnsafe(mergedState, cancellationToken);

            return Task.FromResult(mergedState);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ClaudeBridgeStateStoreException)
        {
            throw;
        }
        catch
        {
            throw new ClaudeBridgeStateStoreException(ClaudeBridgeStateStoreFailureKind.Unreadable);
        }
        finally
        {
            if (hasLock)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private ClaudeBridgeState LoadUnsafe()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return ClaudeBridgeState.Empty;
            }

            var fileInfo = new FileInfo(_stateFilePath);
            if (fileInfo.Length is <= 0 or > MaximumStateFileBytes)
            {
                return FailedState(ClaudeBridgeStateStoreFailureKind.Malformed);
            }

            using var stream = new FileStream(
                _stateFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096);
            var persisted = JsonSerializer.Deserialize(
                stream,
                ClaudeBridgeStateJsonContext.Default.PersistedClaudeBridgeState);

            return MapFromPersistence(persisted);
        }
        catch (JsonException)
        {
            return FailedState(ClaudeBridgeStateStoreFailureKind.Malformed);
        }
        catch (IOException)
        {
            return FailedState(ClaudeBridgeStateStoreFailureKind.Unreadable);
        }
        catch (UnauthorizedAccessException)
        {
            return FailedState(ClaudeBridgeStateStoreFailureKind.Unreadable);
        }
    }

    private void SaveUnsafe(
        ClaudeBridgeState state,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        CleanupTemporaryFiles(directory);

        var tempFilePath = Path.Combine(
            directory ?? Environment.CurrentDirectory,
            $"claude-status-v1.json.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(
                    stream,
                    MapToPersistence(state),
                    ClaudeBridgeStateJsonContext.Default.PersistedClaudeBridgeState);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(_stateFilePath))
            {
                File.Replace(tempFilePath, _stateFilePath, null);
            }
            else
            {
                File.Move(tempFilePath, _stateFilePath);
            }
        }
        finally
        {
            TryDelete(tempFilePath);
            CleanupTemporaryFiles(directory);
        }
    }

    private static ClaudeBridgeState Merge(
        ClaudeBridgeState existingState,
        ClaudeBridgeState incomingState) =>
        new(
            SelectWindow(existingState.FiveHour, incomingState.FiveHour),
            SelectWindow(existingState.SevenDay, incomingState.SevenDay));

    private static ClaudeBridgeQuotaWindowObservation? SelectWindow(
        ClaudeBridgeQuotaWindowObservation? existing,
        ClaudeBridgeQuotaWindowObservation? incoming)
    {
        if (incoming is null)
        {
            return existing;
        }

        if (existing is null)
        {
            return incoming;
        }

        return incoming.ObservedAt >= existing.ObservedAt
            ? incoming
            : existing;
    }

    private static PersistedClaudeBridgeState MapToPersistence(
        ClaudeBridgeState state) =>
        new()
        {
            SchemaVersion = CurrentSchemaVersion,
            FiveHour = MapToPersistence(state.FiveHour),
            SevenDay = MapToPersistence(state.SevenDay)
        };

    private static PersistedClaudeBridgeQuotaWindowObservation? MapToPersistence(
        ClaudeBridgeQuotaWindowObservation? observation) =>
        observation is null
            ? null
            : new PersistedClaudeBridgeQuotaWindowObservation
            {
                UsedPercentage = observation.UsedPercentage,
                ResetAtUtc = observation.ResetAt,
                ObservedAtUtc = observation.ObservedAt
            };

    private static ClaudeBridgeState MapFromPersistence(
        PersistedClaudeBridgeState? state)
    {
        if (state is null || state.SchemaVersion != CurrentSchemaVersion)
        {
            return FailedState(ClaudeBridgeStateStoreFailureKind.Malformed);
        }

        return new ClaudeBridgeState(
            MapFromPersistence(state.FiveHour),
            MapFromPersistence(state.SevenDay));
    }

    private static ClaudeBridgeState FailedState(ClaudeBridgeStateStoreFailureKind failureKind) =>
        new(null, null, failureKind);

    private static ClaudeBridgeQuotaWindowObservation? MapFromPersistence(
        PersistedClaudeBridgeQuotaWindowObservation? observation)
    {
        try
        {
            if (observation?.UsedPercentage is not { } usedPercentage ||
                observation.ObservedAtUtc is not { } observedAt)
            {
                return null;
            }

            return new ClaudeBridgeQuotaWindowObservation(
                usedPercentage,
                observation.ResetAtUtc,
                observedAt);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void CleanupTemporaryFiles(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(
                directory,
                "claude-status-v1.json.*.tmp",
                SearchOption.TopDirectoryOnly))
            {
                TryDelete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
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

internal sealed record PersistedClaudeBridgeState
{
    public int SchemaVersion { get; init; }

    public PersistedClaudeBridgeQuotaWindowObservation? FiveHour { get; init; }

    public PersistedClaudeBridgeQuotaWindowObservation? SevenDay { get; init; }
}

internal sealed record PersistedClaudeBridgeQuotaWindowObservation
{
    public decimal? UsedPercentage { get; init; }

    public DateTimeOffset? ResetAtUtc { get; init; }

    public DateTimeOffset? ObservedAtUtc { get; init; }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PersistedClaudeBridgeState))]
internal sealed partial class ClaudeBridgeStateJsonContext : JsonSerializerContext;
