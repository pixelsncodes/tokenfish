using System.Globalization;
using System.Text;
using System.Text.Json;
using TokenFish.Providers.Codex.RateLimits;
using TokenFish.Providers.Codex.Usage;

namespace TokenFish.Providers.Codex;

public sealed class CodexAppServerProtocolClient : ICodexAppServerProtocolClient
{
    private const int MaximumResponseLineLength = 1_048_576;

    private readonly TextReader _reader;
    private readonly TextWriter _writer;
    private readonly string _clientVersion;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CodexRateLimitsResponseParser _rateLimitsParser = new();
    private readonly CodexAccountUsageResponseParser _accountUsageParser = new();

    private long _nextRequestId = 1;
    private bool _initializeAttempted;
    private bool _initialized;

    public CodexAppServerProtocolClient(
        TextReader reader,
        TextWriter writer,
        string clientVersion)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);

        _clientVersion = clientVersion;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initializeAttempted)
            {
                throw new CodexAppServerProtocolException(
                    "Codex app server initialization was already attempted.");
            }

            _initializeAttempted = true;
            var requestId = GetNextRequestId();

            await WriteJsonLineAsync(
                CreateInitializeRequestJson(requestId, _clientVersion),
                cancellationToken).ConfigureAwait(false);

            await ReadExpectedResponseLineAsync(requestId, cancellationToken).ConfigureAwait(false);

            await WriteJsonLineAsync(
                CreateInitializedNotificationJson(),
                cancellationToken).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CodexRateLimitsSnapshot> ReadRateLimitsAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized();

            var requestId = GetNextRequestId();
            await WriteRequestWithoutParamsAsync(
                requestId,
                "account/rateLimits/read",
                cancellationToken).ConfigureAwait(false);

            var responseLine = await ReadExpectedResponseLineAsync(requestId, cancellationToken)
                .ConfigureAwait(false);

            return _rateLimitsParser.Parse(
                responseLine,
                requestId.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CodexAccountUsageSnapshot> ReadAccountUsageAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized();

            var requestId = GetNextRequestId();
            await WriteRequestWithoutParamsAsync(
                requestId,
                "account/usage/read",
                cancellationToken).ConfigureAwait(false);

            var responseLine = await ReadExpectedResponseLineAsync(requestId, cancellationToken)
                .ConfigureAwait(false);

            return _accountUsageParser.Parse(
                responseLine,
                requestId.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new CodexAppServerProtocolException(
                "Codex app server client is not initialized.");
        }
    }

    private long GetNextRequestId()
    {
        try
        {
            checked
            {
                return _nextRequestId++;
            }
        }
        catch (OverflowException exception)
        {
            throw new CodexAppServerProtocolException(
                "Codex app server request ID could not be allocated.",
                exception);
        }
    }

    private Task WriteRequestWithoutParamsAsync(
        long requestId,
        string method,
        CancellationToken cancellationToken) =>
        WriteJsonLineAsync(
            CreateRequestWithoutParamsJson(requestId, method),
            cancellationToken);

    private async Task WriteJsonLineAsync(
        string line,
        CancellationToken cancellationToken)
    {
        await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string CreateInitializeRequestJson(long requestId, string clientVersion)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", requestId);
            writer.WriteString("method", "initialize");
            writer.WritePropertyName("params");
            writer.WriteStartObject();
            writer.WritePropertyName("clientInfo");
            writer.WriteStartObject();
            writer.WriteString("name", "tokenfish");
            writer.WriteString("title", "TokenFish");
            writer.WriteString("version", clientVersion);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateInitializedNotificationJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("method", "initialized");
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateRequestWithoutParamsJson(long requestId, string method)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", requestId);
            writer.WriteString("method", method);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task<string> ReadExpectedResponseLineAsync(
        long expectedRequestId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await ReadLineWithLimitAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                throw new CodexAppServerProtocolException(
                    "Codex app server stream ended before the expected response.");
            }

            using var document = ParseMessage(line);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw CreateMalformedResponseException();
            }

            var hasId = root.TryGetProperty("id", out var idElement);
            var hasMethod = root.TryGetProperty("method", out _);

            if (!hasId && hasMethod)
            {
                continue;
            }

            if (hasId && hasMethod)
            {
                throw new CodexAppServerProtocolException(
                    "Codex app server sent an unexpected request.");
            }

            if (!hasId)
            {
                throw CreateMalformedResponseException();
            }

            if (!RequestIdMatches(idElement, expectedRequestId))
            {
                throw new CodexAppServerProtocolException(
                    "Codex app server response ID did not match the expected request ID.");
            }

            if (root.TryGetProperty("error", out _))
            {
                throw new CodexAppServerProtocolException(
                    "Codex app server returned an error response.");
            }

            if (!root.TryGetProperty("result", out _))
            {
                throw CreateMalformedResponseException();
            }

            return line;
        }
    }

    private static JsonDocument ParseMessage(string line)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException exception)
        {
            throw CreateMalformedResponseException(exception);
        }
    }

    private static bool RequestIdMatches(JsonElement idElement, long expectedRequestId)
    {
        if (idElement.ValueKind == JsonValueKind.Number &&
            idElement.TryGetInt64(out var numericRequestId))
        {
            return numericRequestId == expectedRequestId;
        }

        if (idElement.ValueKind == JsonValueKind.String &&
            long.TryParse(
                idElement.GetString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var stringRequestId))
        {
            return stringRequestId == expectedRequestId;
        }

        throw CreateMalformedResponseException();
    }

    private async Task<string?> ReadLineWithLimitAsync(CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[1];

        while (true)
        {
            var read = await _reader.ReadAsync(buffer.AsMemory(0, 1), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return builder.Length == 0 ? null : builder.ToString();
            }

            var character = buffer[0];
            if (character == '\n')
            {
                if (builder.Length > 0 && builder[^1] == '\r')
                {
                    builder.Length--;
                }

                return builder.ToString();
            }

            if (builder.Length >= MaximumResponseLineLength)
            {
                throw new CodexAppServerProtocolException(
                    "Codex app server response exceeded the maximum line length.");
            }

            builder.Append(character);
        }
    }

    private static CodexAppServerProtocolException CreateMalformedResponseException(
        Exception? innerException = null) =>
        new("Codex app server response was malformed.", innerException);
}
