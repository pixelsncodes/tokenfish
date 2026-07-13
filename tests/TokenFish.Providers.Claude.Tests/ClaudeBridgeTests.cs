using System.Globalization;
using System.Text;
using System.Text.Json;
using TokenFish.Providers.Claude;

namespace TokenFish.Providers.Claude.Tests;

public sealed class ClaudeBridgeTests
{
    private static readonly DateTimeOffset NewerObservedAt =
        new(2026, 7, 12, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OlderObservedAt =
        NewerObservedAt.AddMinutes(-5);

    [Fact]
    public void RendererCreatesValidSafeStatusLine()
    {
        var output = Render(new ClaudeBridgeState(
            Window(24m),
            Window(41m)));

        Assert.Equal("TokenFish | Claude 5h 24% | 7d 41%", output);
    }

    [Fact]
    public void RendererCreatesPartialWindowOutput()
    {
        Assert.Equal(
            "TokenFish | Claude 5h 24%",
            Render(new ClaudeBridgeState(Window(24m), null)));
        Assert.Equal(
            "TokenFish | 7d 41%",
            Render(new ClaudeBridgeState(null, Window(41m))));
    }

    [Fact]
    public void RendererCreatesNoWindowOutput()
    {
        Assert.Equal("TokenFish | Claude usage unavailable", Render(ClaudeBridgeState.Empty));
    }

    [Fact]
    public void RendererUsesInvariantPercentageFormatting()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            var output = Render(new ClaudeBridgeState(Window(24.5m), null));

            Assert.Equal("TokenFish | Claude 5h 24.5%", output);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task OversizedStdinIsRejectedWithoutWriting()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");
        var input = new MemoryStream(new byte[(1024 * 1024) + 1]);

        var result = await RunBridgeAsync(stateFile, input, "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("TokenFish | Claude usage unavailable", result.Output);
        Assert.False(File.Exists(stateFile));
    }

    [Fact]
    public async Task MalformedInputDoesNotOverwriteValidState()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");
        var store = new ClaudeBridgeStateFileStore(stateFile);
        await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(24m), Window(41m)),
            CancellationToken.None);

        var result = await RunBridgeAsync(stateFile, """{"rate_limits":""");
        var state = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("TokenFish | Claude usage unavailable", result.Output);
        Assert.Equal(24m, state.FiveHour!.UsedPercentage);
        Assert.Equal(41m, state.SevenDay!.UsedPercentage);
    }

    [Fact]
    public async Task NoWindowInputDoesNotEraseExistingObservations()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");
        var store = new ClaudeBridgeStateFileStore(stateFile);
        await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(24m), Window(41m)),
            CancellationToken.None);

        var result = await RunBridgeAsync(stateFile, """{"rate_limits":{}}""");
        var state = await store.LoadAsync(CancellationToken.None);

        Assert.Equal("TokenFish | Claude usage unavailable", result.Output);
        Assert.Equal(24m, state.FiveHour!.UsedPercentage);
        Assert.Equal(41m, state.SevenDay!.UsedPercentage);
    }

    [Fact]
    public async Task FiveHourOnlyUpdatePreservesSevenDayState()
    {
        using var temp = new TemporaryDirectory();
        var store = new ClaudeBridgeStateFileStore(temp.GetPath("claude-status-v1.json"));
        await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(10m), Window(41m)),
            CancellationToken.None);

        var state = await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(24m, NewerObservedAt), null),
            CancellationToken.None);

        Assert.Equal(24m, state.FiveHour!.UsedPercentage);
        Assert.Equal(41m, state.SevenDay!.UsedPercentage);
    }

    [Fact]
    public async Task SevenDayOnlyUpdatePreservesFiveHourState()
    {
        using var temp = new TemporaryDirectory();
        var store = new ClaudeBridgeStateFileStore(temp.GetPath("claude-status-v1.json"));
        await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(24m), Window(10m)),
            CancellationToken.None);

        var state = await store.MergeAndSaveAsync(
            new ClaudeBridgeState(null, Window(41m, NewerObservedAt)),
            CancellationToken.None);

        Assert.Equal(24m, state.FiveHour!.UsedPercentage);
        Assert.Equal(41m, state.SevenDay!.UsedPercentage);
    }

    [Fact]
    public async Task OlderFiveHourObservationCannotReplaceNewerState()
    {
        using var temp = new TemporaryDirectory();
        var store = new ClaudeBridgeStateFileStore(temp.GetPath("claude-status-v1.json"));
        await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(24m, NewerObservedAt), null),
            CancellationToken.None);

        var state = await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(99m, OlderObservedAt), null),
            CancellationToken.None);

        Assert.Equal(24m, state.FiveHour!.UsedPercentage);
    }

    [Fact]
    public async Task OlderSevenDayObservationCannotReplaceNewerState()
    {
        using var temp = new TemporaryDirectory();
        var store = new ClaudeBridgeStateFileStore(temp.GetPath("claude-status-v1.json"));
        await store.MergeAndSaveAsync(
            new ClaudeBridgeState(null, Window(41m, NewerObservedAt)),
            CancellationToken.None);

        var state = await store.MergeAndSaveAsync(
            new ClaudeBridgeState(null, Window(99m, OlderObservedAt)),
            CancellationToken.None);

        Assert.Equal(41m, state.SevenDay!.UsedPercentage);
    }

    [Fact]
    public async Task EqualTimestampReplacementIsDeterministic()
    {
        using var temp = new TemporaryDirectory();
        var store = new ClaudeBridgeStateFileStore(temp.GetPath("claude-status-v1.json"));
        await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(24m, NewerObservedAt), null),
            CancellationToken.None);

        var state = await store.MergeAndSaveAsync(
            new ClaudeBridgeState(Window(25m, NewerObservedAt), null),
            CancellationToken.None);

        Assert.Equal(25m, state.FiveHour!.UsedPercentage);
    }

    [Fact]
    public async Task ConcurrentWritersProduceValidFinalFile()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");

        await Task.WhenAll(
            Enumerable.Range(0, 16).Select(index =>
            {
                var store = new ClaudeBridgeStateFileStore(stateFile);
                return store.MergeAndSaveAsync(
                    new ClaudeBridgeState(
                        Window(index, NewerObservedAt.AddSeconds(index)),
                        Window(100 - index, NewerObservedAt.AddSeconds(index))),
                    CancellationToken.None);
            }));

        var json = await File.ReadAllTextAsync(stateFile);
        using var document = JsonDocument.Parse(json);
        var state = await new ClaudeBridgeStateFileStore(stateFile).LoadAsync(CancellationToken.None);

        Assert.Equal(15m, state.FiveHour!.UsedPercentage);
        Assert.Equal(85m, state.SevenDay!.UsedPercentage);
    }

    [Fact]
    public async Task ReplacementProducesValidReadableFiles()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");
        var store = new ClaudeBridgeStateFileStore(stateFile);

        for (var index = 0; index < 50; index++)
        {
            await store.MergeAndSaveAsync(
                new ClaudeBridgeState(Window(index, NewerObservedAt.AddSeconds(index)), null),
                CancellationToken.None);

            var state = await store.LoadAsync(CancellationToken.None);
            Assert.NotNull(state.FiveHour);
        }
    }

    [Fact]
    public async Task CorruptExistingFileCanBeReplacedByLaterValidSample()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");
        await File.WriteAllTextAsync(stateFile, "{not-json");

        var state = await new ClaudeBridgeStateFileStore(stateFile).MergeAndSaveAsync(
            new ClaudeBridgeState(Window(24m), null),
            CancellationToken.None);

        Assert.Equal(24m, state.FiveHour!.UsedPercentage);
        Assert.Contains("schemaVersion", await File.ReadAllTextAsync(stateFile));
    }

    [Fact]
    public async Task UnknownSchemaVersionIsHandledSafely()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");
        await File.WriteAllTextAsync(stateFile, """{"schemaVersion":999}""");

        var state = await new ClaudeBridgeStateFileStore(stateFile).LoadAsync(CancellationToken.None);

        Assert.False(state.HasUsageData);
    }

    [Fact]
    public async Task TemporaryFilesAreCleanedUp()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");
        await File.WriteAllTextAsync(temp.GetPath("claude-status-v1.json.synthetic.tmp"), "");

        await new ClaudeBridgeStateFileStore(stateFile).MergeAndSaveAsync(
            new ClaudeBridgeState(Window(24m), null),
            CancellationToken.None);

        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task DryRunPerformsNoWrite()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");

        var result = await RunBridgeAsync(
            stateFile,
            """{"rate_limits":{"five_hour":{"used_percentage":24}}}""",
            "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("TokenFish | Claude 5h 24%", result.Output);
        Assert.False(File.Exists(stateFile));
    }

    [Fact]
    public async Task UnknownCommandLineArgumentsAreRejectedSafely()
    {
        using var temp = new TemporaryDirectory();
        var result = await RunBridgeAsync(
            temp.GetPath("claude-status-v1.json"),
            """{"rate_limits":{"five_hour":{"used_percentage":24}}}""",
            "--output",
            "synthetic-path");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("TokenFish | Claude usage unavailable", result.Output);
    }

    [Fact]
    public async Task OutputAndPersistedJsonExcludeForbiddenNamesAndValues()
    {
        using var temp = new TemporaryDirectory();
        var stateFile = temp.GetPath("claude-status-v1.json");
        var payload = """
            {
              "cwd": "C:\\Synthetic\\Repo",
              "workspace": "/synthetic/workspace",
              "session_id": "synthetic-session-id",
              "prompt_id": "synthetic-prompt-id",
              "transcript_path": "/synthetic/transcript.jsonl",
              "model": "synthetic-model",
              "cost": { "usd": 9.99 },
              "rate_limits": {
                "five_hour": { "used_percentage": 24 },
                "seven_day": { "used_percentage": 41 }
              }
            }
            """;

        var result = await RunBridgeAsync(stateFile, payload);
        var persisted = await File.ReadAllTextAsync(stateFile);
        var combined = result.Output + persisted;

        Assert.Equal("TokenFish | Claude 5h 24% | 7d 41%", result.Output);
        foreach (var forbidden in new[]
        {
            "cwd",
            "workspace",
            "session_id",
            "prompt_id",
            "transcript_path",
            "model",
            "cost",
            "Synthetic",
            "synthetic-session-id",
            "synthetic-prompt-id"
        })
        {
            Assert.DoesNotContain(forbidden, combined);
        }
    }

    [Fact]
    public void ExceptionsDoNotIncludeInputJsonOrLocalPaths()
    {
        var parseException = Assert.Throws<ClaudeStatusLineUsageParseException>(() =>
            new ClaudeStatusLineUsageParser().Parse(
                """{"rate_limits":{"five_hour":{"used_percentage":"synthetic-secret"}}}""",
                TimeProvider.System));
        var storeException = new ClaudeBridgeStateStoreException();

        Assert.DoesNotContain("synthetic-secret", parseException.ToString());
        Assert.DoesNotContain("rate_limits", parseException.ToString());
        Assert.DoesNotContain("C:\\", storeException.ToString());
        Assert.DoesNotContain("/synthetic", storeException.ToString());
    }

    [Fact]
    public void DefaultApplicationDataPathResolutionIsTestable()
    {
        var resolver = new ClaudeBridgeStateFilePathResolver(
            _ => Path.Combine("X:", "SyntheticLocalAppData"));

        Assert.Equal(
            Path.Combine("X:", "SyntheticLocalAppData", "TokenFish", "bridge", "claude-status-v1.json"),
            resolver.GetDefaultStateFilePath());
    }

    private static string Render(ClaudeBridgeState state) =>
        new ClaudeBridgeStatusLineRenderer().Render(state);

    private static ClaudeBridgeQuotaWindowObservation Window(
        decimal usedPercentage,
        DateTimeOffset? observedAt = null) =>
        new(
            usedPercentage,
            resetAt: new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero),
            observedAt ?? NewerObservedAt);

    private static Task<BridgeRunResult> RunBridgeAsync(
        string stateFile,
        string input,
        params string[] args) =>
        RunBridgeAsync(
            stateFile,
            new MemoryStream(Encoding.UTF8.GetBytes(input)),
            args);

    private static async Task<BridgeRunResult> RunBridgeAsync(
        string stateFile,
        Stream input,
        params string[] args)
    {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new ClaudeBridgeConsoleRunner(
            new ClaudeBridgeStateFileStore(stateFile),
            new ManualTimeProvider(NewerObservedAt));

        var exitCode = await runner.RunAsync(
            args,
            input,
            output,
            CancellationToken.None);

        return new BridgeRunResult(exitCode, output.ToString().TrimEnd());
    }

    private sealed record BridgeRunResult(int ExitCode, string Output);

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
                "TokenFishClaudeBridgeTests",
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
