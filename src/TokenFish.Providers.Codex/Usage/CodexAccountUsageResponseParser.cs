using System.Globalization;
using System.Text.Json;

namespace TokenFish.Providers.Codex.Usage;

public sealed class CodexAccountUsageResponseParser
{
    public CodexAccountUsageSnapshot Parse(string jsonRpcResponseLine, string expectedRequestId)
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
                throw new CodexAccountUsageResponseParseException(
                    "Codex account usage response ID did not match the expected request ID.");
            }

            if (root.TryGetProperty("error", out var errorElement))
            {
                throw CreateJsonRpcErrorException(errorElement);
            }

            if (!root.TryGetProperty("result", out var resultElement) ||
                resultElement.ValueKind != JsonValueKind.Object ||
                !resultElement.TryGetProperty("summary", out var summaryElement) ||
                summaryElement.ValueKind != JsonValueKind.Object)
            {
                throw CreateMalformedResponseException();
            }

            return new CodexAccountUsageSnapshot(ParseDailyUsageBuckets(resultElement))
            {
                LifetimeTokens = ReadOptionalSummaryValue(summaryElement,"lifetimeTokens"),
                PeakDailyTokens = ReadOptionalSummaryValue(summaryElement,"peakDailyTokens"),
                LongestRunningTurnSec = ReadOptionalSummaryValue(summaryElement,"longestRunningTurnSec"),
                CurrentStreakDays = ReadOptionalSummaryValue(summaryElement,"currentStreakDays"),
                LongestStreakDays = ReadOptionalSummaryValue(summaryElement,"longestStreakDays")
            };
        }
        catch (CodexAccountUsageResponseParseException)
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

    private static IReadOnlyList<CodexDailyTokenUsage>? ParseDailyUsageBuckets(JsonElement resultElement)
    {
        if (!resultElement.TryGetProperty("dailyUsageBuckets", out var bucketsElement) ||
            bucketsElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (bucketsElement.ValueKind != JsonValueKind.Array)
        {
            throw CreateMalformedResponseException();
        }

        var buckets = new List<CodexDailyTokenUsage>();
        var observedDates = new HashSet<DateOnly>();

        foreach (var bucketElement in bucketsElement.EnumerateArray())
        {
            if (bucketElement.ValueKind != JsonValueKind.Object)
            {
                throw CreateMalformedResponseException();
            }

            var startDate = ReadRequiredDate(bucketElement, "startDate");
            if (!observedDates.Add(startDate))
            {
                throw new CodexAccountUsageResponseParseException(
                    "Codex account usage response contained duplicate daily usage dates.");
            }

            var tokens = ReadRequiredInt64(bucketElement, "tokens");
            if (tokens < 0)
            {
                throw new CodexAccountUsageResponseParseException(
                    "Codex account usage response contained an invalid token count.");
            }

            buckets.Add(new CodexDailyTokenUsage(startDate, tokens));
        }

        return buckets;
    }

    private static long? ReadOptionalSummaryValue(JsonElement summary,string name)
    {
        if(!summary.TryGetProperty(name,out var value)||value.ValueKind==JsonValueKind.Null) return null;
        // Optional fields from newer runtimes must not make otherwise usable activity disappear.
        return value.ValueKind==JsonValueKind.Number && value.TryGetInt64(out var result) && result>=0 ? result : null;
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

    private static DateOnly ReadRequiredDate(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.String ||
            !DateOnly.TryParseExact(
                valueElement.GetString(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw new CodexAccountUsageResponseParseException(
                "Codex account usage response contained an invalid daily usage date.");
        }

        return date;
    }

    private static long ReadRequiredInt64(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.Number)
        {
            throw CreateMalformedResponseException();
        }

        return valueElement.GetInt64();
    }

    private static CodexAccountUsageResponseParseException CreateJsonRpcErrorException(JsonElement errorElement)
    {
        var errorCode = errorElement.ValueKind == JsonValueKind.Object &&
            errorElement.TryGetProperty("code", out var codeElement) &&
            codeElement.ValueKind == JsonValueKind.Number
                ? codeElement.GetInt64()
                : null as long?;

        return new CodexAccountUsageResponseParseException(
            "Codex account usage request failed.",
            errorCode);
    }

    private static CodexAccountUsageResponseParseException CreateMalformedResponseException(
        Exception? innerException = null) =>
        new("Codex account usage response was malformed.", innerException: innerException);
}
