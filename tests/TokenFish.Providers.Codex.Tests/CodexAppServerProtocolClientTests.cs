using System.Text;
using System.Text.Json;

namespace TokenFish.Providers.Codex.Tests;

public sealed class CodexAppServerProtocolClientTests
{
    [Fact]
    public async Task InitializeWritesValidInitializeRequest()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient([InitializeResponse(1)], writer);

        await client.InitializeAsync(CancellationToken.None);

        using var document = JsonDocument.Parse(writer.Lines[0]);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("id").GetInt64());
        Assert.Equal("initialize", root.GetProperty("method").GetString());
        Assert.True(root.TryGetProperty("params", out _));
    }

    [Fact]
    public async Task ClientMetadataIsCorrect()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient([InitializeResponse(1)], writer, clientVersion: "1.2.3");

        await client.InitializeAsync(CancellationToken.None);

        using var document = JsonDocument.Parse(writer.Lines[0]);
        var clientInfo = document.RootElement
            .GetProperty("params")
            .GetProperty("clientInfo");
        Assert.Equal("tokenfish", clientInfo.GetProperty("name").GetString());
        Assert.Equal("TokenFish", clientInfo.GetProperty("title").GetString());
        Assert.Equal("1.2.3", clientInfo.GetProperty("version").GetString());
    }

    [Fact]
    public async Task InitializeSendsInitializedAfterSuccessfulResponse()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient([InitializeResponse(1)], writer);

        await client.InitializeAsync(CancellationToken.None);

        Assert.Equal(2, writer.Lines.Count);
        using var document = JsonDocument.Parse(writer.Lines[1]);
        var root = document.RootElement;
        Assert.Equal("initialized", root.GetProperty("method").GetString());
        Assert.False(root.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task OutboundMessagesAreNewlineDelimitedAndFlushed()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient([InitializeResponse(1)], writer);

        await client.InitializeAsync(CancellationToken.None);

        Assert.Equal(2, writer.FlushCount);
        Assert.EndsWith("\n", writer.Text);
        Assert.Equal(2, writer.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task RequestIdsIncreaseMonotonically()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient(
            [
                InitializeResponse(1),
                RateLimitsResponse(2),
                AccountUsageResponse(3)
            ],
            writer);

        await client.InitializeAsync(CancellationToken.None);
        await client.ReadRateLimitsAsync(CancellationToken.None);
        await client.ReadAccountUsageAsync(CancellationToken.None);

        Assert.Equal([1L, 2L, 3L], writer.Lines.Where(HasRequestId).Select(ReadRequestId));
    }

    [Fact]
    public async Task RateLimitRequestUsesAccountRateLimitsRead()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient([InitializeResponse(1), RateLimitsResponse(2)], writer);
        await client.InitializeAsync(CancellationToken.None);

        await client.ReadRateLimitsAsync(CancellationToken.None);

        Assert.Equal("account/rateLimits/read", ReadMethod(writer.Lines[2]));
        Assert.False(HasParams(writer.Lines[2]));
    }

    [Fact]
    public async Task UsageRequestUsesAccountUsageRead()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient([InitializeResponse(1), AccountUsageResponse(2)], writer);
        await client.InitializeAsync(CancellationToken.None);

        await client.ReadAccountUsageAsync(CancellationToken.None);

        Assert.Equal("account/usage/read", ReadMethod(writer.Lines[2]));
        Assert.False(HasParams(writer.Lines[2]));
    }

    [Fact]
    public async Task ExistingParsersReceiveAndReturnValidResults()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient(
            [
                InitializeResponse(1),
                RateLimitsResponse(2, primaryUsedPercent: 67),
                AccountUsageResponse(3, tokens: 987)
            ],
            writer);
        await client.InitializeAsync(CancellationToken.None);

        var rateLimits = await client.ReadRateLimitsAsync(CancellationToken.None);
        var usage = await client.ReadAccountUsageAsync(CancellationToken.None);

        Assert.Equal(67, rateLimits.Primary.UsedPercent);
        Assert.Equal(987, Assert.Single(usage.DailyUsageBuckets!).Tokens);
    }

    [Fact]
    public async Task RequestsBeforeInitializationAreRejected()
    {
        var client = CreateClient([], new RecordingTextWriter());

        await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.ReadRateLimitsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SecondInitializationIsRejected()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient([InitializeResponse(1)], writer);
        await client.InitializeAsync(CancellationToken.None);

        await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.InitializeAsync(CancellationToken.None));

        Assert.Single(writer.Lines, line => ReadMethod(line) == "initialize");
    }

    [Fact]
    public async Task NotificationsBeforeAResponseAreIgnored()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient(
            [
                Notification("account/rateLimits/updated"),
                InitializeResponse(1)
            ],
            writer);

        await client.InitializeAsync(CancellationToken.None);

        Assert.Equal("initialized", ReadMethod(writer.Lines[1]));
    }

    [Fact]
    public async Task UnexpectedServerRequestIsRejected()
    {
        var client = CreateClient(
            ["""{"id":99,"method":"item/tool/requestUserInput","params":{"fixture":"SHOULD_NOT_APPEAR"}}"""],
            new RecordingTextWriter());

        var exception = await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.InitializeAsync(CancellationToken.None));
        Assert.DoesNotContain("SHOULD_NOT_APPEAR", exception.Message);
    }

    [Fact]
    public async Task MismatchedResponseIdIsRejected()
    {
        var client = CreateClient([InitializeResponse(99)], new RecordingTextWriter());

        await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task JsonRpcErrorResponseIsSafelyRejected()
    {
        const string fixture = "SENSITIVE_FIXTURE_PAYLOAD";
        var client = CreateClient(
            ["{\"id\":1,\"error\":{\"code\":-32000,\"message\":\"" + fixture + "\"}}"],
            new RecordingTextWriter());

        var exception = await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.InitializeAsync(CancellationToken.None));
        Assert.DoesNotContain(fixture, exception.Message);
    }

    [Fact]
    public async Task MalformedJsonIsRejected()
    {
        var client = CreateClient(["{not valid json"], new RecordingTextWriter());

        await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task EndOfStreamIsRejected()
    {
        var client = CreateClient([], new RecordingTextWriter());

        await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OversizedResponseLineIsRejected()
    {
        var oversizedLine = new string(' ', 1_048_577);
        var client = CreateClient([oversizedLine], new RecordingTextWriter());

        await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.InitializeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        var writer = new RecordingTextWriter();
        var client = new CodexAppServerProtocolClient(
            new BlockingTextReader(),
            writer,
            "1.0.0");
        using var cancellation = new CancellationTokenSource();

        var task = client.InitializeAsync(cancellation.Token);
        await WaitForWrittenLineAsync(writer);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task ConcurrentOperationsDoNotCorruptRequestOrResponseOrdering()
    {
        var writer = new RecordingTextWriter();
        var client = CreateClient(
            [
                InitializeResponse(1),
                RateLimitsResponse(2, primaryUsedPercent: 11),
                AccountUsageResponse(3, tokens: 22)
            ],
            writer);
        await client.InitializeAsync(CancellationToken.None);

        var rateLimitsTask = client.ReadRateLimitsAsync(CancellationToken.None);
        var usageTask = client.ReadAccountUsageAsync(CancellationToken.None);
        var rateLimits = await rateLimitsTask;
        var usage = await usageTask;

        Assert.Equal(11, rateLimits.Primary.UsedPercent);
        Assert.Equal(22, Assert.Single(usage.DailyUsageBuckets!).Tokens);
        Assert.Equal(
            ["initialize", "initialized", "account/rateLimits/read", "account/usage/read"],
            writer.Lines.Select(ReadMethod));
        Assert.Equal([1L, 2L, 3L], writer.Lines.Where(HasRequestId).Select(ReadRequestId));
    }

    [Fact]
    public async Task ExceptionsDoNotContainRawSyntheticFixturePayloads()
    {
        const string fixture = "RAW_SYNTHETIC_FIXTURE_PAYLOAD";
        var client = CreateClient(
            ["{\"id\":1,\"result\":{\"fixture\":\"" + fixture + "\"},\"unexpected\":}"],
            new RecordingTextWriter());

        var exception = await Assert.ThrowsAsync<CodexAppServerProtocolException>(() =>
            client.InitializeAsync(CancellationToken.None));
        Assert.DoesNotContain(fixture, exception.Message);
    }

    private static CodexAppServerProtocolClient CreateClient(
        IReadOnlyList<string> responseLines,
        RecordingTextWriter writer,
        string clientVersion = "1.0.0") =>
        new(new StringReader(string.Join('\n', responseLines)), writer, clientVersion);

    private static string InitializeResponse(long id) =>
        "{\"id\":" + id + ",\"result\":{}}";

    private static string RateLimitsResponse(long id, int primaryUsedPercent = 55) =>
        "{\"id\":" + id + ",\"result\":{\"rateLimits\":{\"primary\":{\"usedPercent\":" +
        primaryUsedPercent + ",\"windowDurationMins\":300,\"resetsAt\":1785000000}," +
        "\"secondary\":{\"usedPercent\":21,\"windowDurationMins\":10080,\"resetsAt\":1785604800}," +
        "\"rateLimitReachedType\":\"rate_limit_reached\"}}}";

    private static string AccountUsageResponse(long id, long tokens = 123) =>
        "{\"id\":" + id + ",\"result\":{\"dailyUsageBuckets\":[{\"startDate\":\"2026-07-12\"," +
        "\"tokens\":" + tokens + "}],\"summary\":{\"lifetimeTokens\":999,\"peakDailyTokens\":" +
        tokens + "}}}";

    private static string Notification(string method) =>
        "{\"method\":\"" + method + "\",\"params\":{\"id\":1,\"ignored\":true}}";

    private static long ReadRequestId(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("id").GetInt64();
    }

    private static string? ReadMethod(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("method").GetString();
    }

    private static bool HasRequestId(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty("id", out _);
    }

    private static bool HasParams(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.TryGetProperty("params", out _);
    }

    private static async Task WaitForWrittenLineAsync(RecordingTextWriter writer)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (writer.Lines.Count > 0)
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    private sealed class RecordingTextWriter : TextWriter
    {
        private readonly StringBuilder _text = new();
        private readonly object _syncRoot = new();

        public override Encoding Encoding => Encoding.UTF8;

        public List<string> Lines { get; } = [];

        public int FlushCount { get; private set; }

        public string Text
        {
            get
            {
                lock (_syncRoot)
                {
                    return _text.ToString();
                }
            }
        }

        public override Task WriteLineAsync(
            ReadOnlyMemory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = buffer.ToString();
            lock (_syncRoot)
            {
                Lines.Add(line);
                _text.Append(line);
                _text.Append('\n');
            }

            return Task.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                FlushCount++;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class BlockingTextReader : TextReader
    {
        public override async ValueTask<int> ReadAsync(
            Memory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
