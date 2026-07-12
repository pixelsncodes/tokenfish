using System.Text.Json;
using System.Globalization;

namespace TokenFish.Providers.Codex.RateLimits;

public sealed class CodexRateLimitsResponseParser
{
    public CodexRateLimitsSnapshot Parse(string jsonRpcResponseLine, string expectedRequestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonRpcResponseLine);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRequestId);

        try
        {
            using var document = JsonDocument.Parse(jsonRpcResponseLine);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw CreateMalformedResponseException();
            }

            var actualRequestId = ReadRequiredRequestId(root);
            if (!StringComparer.Ordinal.Equals(actualRequestId, expectedRequestId))
            {
                throw new CodexRateLimitsResponseParseException(
                    "Codex rate limits response ID did not match the expected request ID.");
            }

            if (root.TryGetProperty("error", out var errorElement))
            {
                throw CreateJsonRpcErrorException(errorElement);
            }

            if (!root.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Object ||
                !resultElement.TryGetProperty("rateLimits", out var rateLimitsElement) ||
                rateLimitsElement.ValueKind != JsonValueKind.Object)
            {
                throw CreateMalformedResponseException();
            }

            var defaultBucket = ParseBucket(rateLimitsElement);

            return new CodexRateLimitsSnapshot(
                defaultBucket.LimitId,
                defaultBucket.LimitName,
                defaultBucket.Primary,
                defaultBucket.Secondary,
                defaultBucket.RateLimitReachedType,
                ParseRateLimitsByLimitId(resultElement));
        }
        catch (CodexRateLimitsResponseParseException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw CreateMalformedResponseException(exception);
        }
        catch (InvalidOperationException exception)
        {
            throw CreateMalformedResponseException(exception);
        }
        catch (FormatException exception)
        {
            throw CreateMalformedResponseException(exception);
        }
    }

    private static string ReadRequiredRequestId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var idElement))
        {
            throw CreateMalformedResponseException();
        }

        return idElement.ValueKind switch
        {
            JsonValueKind.String => idElement.GetString() ?? throw CreateMalformedResponseException(),
            JsonValueKind.Number => idElement.GetInt64().ToString(CultureInfo.InvariantCulture),
            _ => throw CreateMalformedResponseException()
        };
    }

    private static CodexRateLimitWindow ParseWindow(JsonElement rateLimitsElement, string propertyName)
    {
        if (!rateLimitsElement.TryGetProperty(propertyName, out var windowElement) ||
            windowElement.ValueKind == JsonValueKind.Null)
        {
            return CodexRateLimitWindow.Unavailable;
        }

        if (windowElement.ValueKind != JsonValueKind.Object ||
            !windowElement.TryGetProperty("usedPercent", out var usedPercentElement) ||
            usedPercentElement.ValueKind != JsonValueKind.Number)
        {
            throw CreateMalformedResponseException();
        }

        var usedPercent = usedPercentElement.GetInt32();
        if (usedPercent is < 0 or > 100)
        {
            throw new CodexRateLimitsResponseParseException(
                "Codex rate limits response contained an invalid used percentage.");
        }

        var windowDurationMins = ReadOptionalInt64(windowElement, "windowDurationMins");
        if (windowDurationMins is <= 0)
        {
            throw new CodexRateLimitsResponseParseException(
                "Codex rate limits response contained an invalid window duration.");
        }

        var resetsAtUnixSeconds = ReadOptionalInt64(windowElement, "resetsAt");
        var resetsAt = resetsAtUnixSeconds.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(resetsAtUnixSeconds.Value)
            : null as DateTimeOffset?;

        return new CodexRateLimitWindow(usedPercent, windowDurationMins, resetsAt);
    }

    private static CodexRateLimitBucket ParseBucket(JsonElement rateLimitsElement) =>
        new(
            ReadOptionalString(rateLimitsElement, "limitId"),
            ReadOptionalString(rateLimitsElement, "limitName"),
            ParseWindow(rateLimitsElement, "primary"),
            ParseWindow(rateLimitsElement, "secondary"),
            ReadOptionalString(rateLimitsElement, "rateLimitReachedType"));

    private static IReadOnlyList<CodexRateLimitBucket> ParseRateLimitsByLimitId(
        JsonElement resultElement)
    {
        if (!resultElement.TryGetProperty("rateLimitsByLimitId", out var byLimitIdElement) ||
            byLimitIdElement.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (byLimitIdElement.ValueKind != JsonValueKind.Object)
        {
            throw CreateMalformedResponseException();
        }

        var buckets = new List<CodexRateLimitBucket>();
        foreach (var property in byLimitIdElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                throw CreateMalformedResponseException();
            }

            var bucket = ParseBucket(property.Value);
            buckets.Add(bucket.LimitId is null
                ? new CodexRateLimitBucket(
                    property.Name,
                    bucket.LimitName,
                    bucket.Primary,
                    bucket.Secondary,
                    bucket.RateLimitReachedType)
                : bucket);
        }

        return buckets;
    }

    private static long? ReadOptionalInt64(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var valueElement) ||
            valueElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (valueElement.ValueKind != JsonValueKind.Number)
        {
            throw CreateMalformedResponseException();
        }

        return valueElement.GetInt64();
    }

    private static string? ReadOptionalString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var valueElement) ||
            valueElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (valueElement.ValueKind != JsonValueKind.String)
        {
            throw CreateMalformedResponseException();
        }

        return valueElement.GetString();
    }

    private static CodexRateLimitsResponseParseException CreateJsonRpcErrorException(JsonElement errorElement)
    {
        var errorCode = errorElement.ValueKind == JsonValueKind.Object &&
            errorElement.TryGetProperty("code", out var codeElement) &&
            codeElement.ValueKind == JsonValueKind.Number
                ? codeElement.GetInt64()
                : null as long?;

        return new CodexRateLimitsResponseParseException(
            "Codex rate limits request failed.",
            errorCode);
    }

    private static CodexRateLimitsResponseParseException CreateMalformedResponseException(
        Exception? innerException = null) =>
        new("Codex rate limits response was malformed.", innerException: innerException);
}
