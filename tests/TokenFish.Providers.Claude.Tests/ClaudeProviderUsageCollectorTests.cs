using TokenFish.Core.Models;
using TokenFish.Providers.Claude;

namespace TokenFish.Providers.Claude.Tests;

public sealed class ClaudeProviderUsageCollectorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 12, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidTwoWindowBridgeStateMapsCorrectly()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(
            Window(24m, Now, resetAt: Now.AddHours(1)),
            Window(41m, Now, resetAt: Now.AddDays(2))));

        Assert.Equal(ProviderKind.Claude, snapshot.Provider);
        Assert.Equal(ProviderConnectionState.Connected, snapshot.ConnectionState);
        Assert.Equal(2, snapshot.QuotaWindows.Count);
        Assert.Equal(["5h", "7d"], snapshot.QuotaWindows.Select(window => window.DisplayLabel));
    }

    [Fact]
    public async Task FiveHourOnlyStateMapsCorrectly()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(Window(24m, Now), null));

        var window = Assert.Single(snapshot.QuotaWindows);
        Assert.Equal("claude:status-line:five-hour", window.WindowId);
        Assert.Equal(24m, window.UsedPercentage);
        Assert.Equal(24m, snapshot.UsageWindow.PercentageConsumed);
    }

    [Fact]
    public async Task SevenDayOnlyStateMapsCorrectly()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(null, Window(41m, Now)));

        var window = Assert.Single(snapshot.QuotaWindows);
        Assert.Equal("claude:status-line:seven-day", window.WindowId);
        Assert.Equal(41m, window.UsedPercentage);
        Assert.False(snapshot.UsageWindow.IsAvailable);
    }

    [Fact]
    public async Task PercentagesRetainProviderReportedAuthority()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(Window(24m, Now), null));

        Assert.Equal(DataAuthority.LocalProviderReported, Assert.Single(snapshot.QuotaWindows).Authority);
    }

    [Fact]
    public async Task ResetTimestampsMapCorrectly()
    {
        var resetAt = Now.AddHours(1);

        var snapshot = await CollectAsync(new ClaudeBridgeState(Window(24m, Now, resetAt), null));

        Assert.Equal(resetAt, Assert.Single(snapshot.QuotaWindows).ResetAt);
        Assert.Equal(resetAt, snapshot.UsageWindowResetAt);
    }

    [Fact]
    public async Task WindowDurationsAreCorrect()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(Window(24m, Now), Window(41m, Now)));

        Assert.Equal(
            [TimeSpan.FromMinutes(300), TimeSpan.FromMinutes(10_080)],
            snapshot.QuotaWindows.Select(window => window.WindowDuration));
    }

    [Fact]
    public async Task ActivityMetricsRemainUnavailable()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(Window(24m, Now), Window(41m, Now)));

        Assert.Empty(snapshot.ActivityMetrics);
        Assert.False(snapshot.SessionTokens.IsAvailable);
        Assert.False(snapshot.WeeklyTokens.IsAvailable);
    }

    [Fact]
    public async Task SevenDayPercentageIsNotTreatedAsWeeklyTokenCount()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(null, Window(41m, Now)));

        Assert.False(snapshot.WeeklyTokens.IsAvailable);
        Assert.DoesNotContain(snapshot.ActivityMetrics, metric => metric.IsAvailable);
    }

    [Fact]
    public async Task FreshSourceObservationsMapLive()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(Window(24m, Now.AddMinutes(-1)), null));

        Assert.Equal(DataFreshness.Live, Assert.Single(snapshot.QuotaWindows).Freshness);
    }

    [Fact]
    public async Task SourceObservationTimeControlsRuntimeFreshness()
    {
        var observedAt = Now.AddMinutes(-1);
        var snapshot = await CollectAsync(new ClaudeBridgeState(Window(24m, observedAt), null));

        Assert.Equal(observedAt, snapshot.SourceObservedAt);
    }

    [Fact]
    public async Task OldSourceObservationsMapStale()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(Window(24m, Now.AddMinutes(-3)), null));

        Assert.Equal(DataFreshness.Stale, Assert.Single(snapshot.QuotaWindows).Freshness);
    }

    [Fact]
    public async Task FutureObservationTimestampsAreRejectedSafely()
    {
        var snapshot = await CollectAsync(new ClaudeBridgeState(
            Window(24m, Now.AddMinutes(3)),
            Window(41m, Now)));

        var window = Assert.Single(snapshot.QuotaWindows);
        Assert.Equal("claude:status-line:seven-day", window.WindowId);
    }

    [Fact]
    public async Task MissingStateFileMapsSafely()
    {
        using var temp = new TemporaryDirectory();
        var collector = CreateCollector(new ClaudeBridgeStateFileStore(temp.GetPath("missing.json")));

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderConnectionState.Disconnected, snapshot.ConnectionState);
        Assert.Empty(snapshot.QuotaWindows);
    }

    [Fact]
    public async Task CorruptStateFileMapsSafely()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.GetPath("claude-status-v1.json");
        await File.WriteAllTextAsync(path, "{not-json");
        var collector = CreateCollector(new ClaudeBridgeStateFileStore(path));

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderConnectionState.Disconnected, snapshot.ConnectionState);
    }

    [Fact]
    public async Task OversizedStateFileMapsSafely()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.GetPath("claude-status-v1.json");
        await File.WriteAllBytesAsync(path, new byte[(64 * 1024) + 1]);
        var collector = CreateCollector(new ClaudeBridgeStateFileStore(path));

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderConnectionState.Disconnected, snapshot.ConnectionState);
    }

    [Fact]
    public async Task UnsupportedSchemaMapsSafely()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.GetPath("claude-status-v1.json");
        await File.WriteAllTextAsync(path, """{"schemaVersion":999}""");
        var collector = CreateCollector(new ClaudeBridgeStateFileStore(path));

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderConnectionState.Disconnected, snapshot.ConnectionState);
    }

    [Fact]
    public async Task OneMalformedWindowDoesNotDiscardIndependentValidWindow()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.GetPath("claude-status-v1.json");
        await File.WriteAllTextAsync(
            path,
            $$"""
            {
              "schemaVersion": 1,
              "fiveHour": { "usedPercentage": 101, "observedAtUtc": "{{Now:O}}" },
              "sevenDay": { "usedPercentage": 41, "observedAtUtc": "{{Now:O}}" }
            }
            """);
        var collector = CreateCollector(new ClaudeBridgeStateFileStore(path));

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        var window = Assert.Single(snapshot.QuotaWindows);
        Assert.Equal("claude:status-line:seven-day", window.WindowId);
        Assert.Equal(41m, window.UsedPercentage);
    }

    [Fact]
    public async Task ExpectedClaudeReadFailureMapsUnavailable()
    {
        var collector = new ClaudeBridgeFailureMappingCollector(
            CreateCollector(new ThrowingStore(new ClaudeBridgeStateStoreException())),
            new ManualTimeProvider(Now));

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.Equal(ProviderConnectionState.Disconnected, snapshot.ConnectionState);
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var collector = CreateCollector(new CancelingStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            collector.CollectAsync(cancellationTokenSource.Token));
    }

    private static ClaudeProviderUsageCollector CreateCollector(IClaudeBridgeStateStore store) =>
        new(
            store,
            new ManualTimeProvider(Now),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(2));

    private static Task<ProviderUsageSnapshot> CollectAsync(ClaudeBridgeState state) =>
        CreateCollector(new FixedStore(state)).CollectAsync(CancellationToken.None);

    private static ClaudeBridgeQuotaWindowObservation Window(
        decimal usedPercentage,
        DateTimeOffset observedAt,
        DateTimeOffset? resetAt = null) =>
        new(usedPercentage, resetAt, observedAt);

    private sealed class FixedStore : IClaudeBridgeStateStore
    {
        private readonly ClaudeBridgeState _state;

        public FixedStore(ClaudeBridgeState state)
        {
            _state = state;
        }

        public Task<ClaudeBridgeState> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_state);

        public Task<ClaudeBridgeState> MergeAndSaveAsync(
            ClaudeBridgeState incomingState,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingStore : IClaudeBridgeStateStore
    {
        private readonly Exception _exception;

        public ThrowingStore(Exception exception)
        {
            _exception = exception;
        }

        public Task<ClaudeBridgeState> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromException<ClaudeBridgeState>(_exception);

        public Task<ClaudeBridgeState> MergeAndSaveAsync(
            ClaudeBridgeState incomingState,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CancelingStore : IClaudeBridgeStateStore
    {
        public Task<ClaudeBridgeState> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromCanceled<ClaudeBridgeState>(cancellationToken);

        public Task<ClaudeBridgeState> MergeAndSaveAsync(
            ClaudeBridgeState incomingState,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "TokenFishClaudeCollectorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string GetPath(string fileName) => System.IO.Path.Combine(Path, fileName);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
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
}
